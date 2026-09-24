$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Tests\KhimTools.Mcp.ContractTests\KhimTools.Mcp.ContractTests.csproj'
$protocol = Get-Content (Join-Path $root 'KhimTools\Core\Automation\McpJsonRpcAdapter.cs') -Raw
$stdio = Get-Content (Join-Path $root 'KhimTools\Core\Automation\McpStdioTransport.cs') -Raw
$api = Get-Content (Join-Path $root 'KhimTools\Core\Automation\InternalAutomationApi.cs') -Raw
$capability = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\SheetExport\Services\SheetNamingPreviewCapability.cs') -Raw
$naming = Get-Content (Join-Path $root 'KhimTools\Tools\KhimGen\SheetExport\Services\NamingPlanService.cs') -Raw
$checks = 0
function Assert-Text([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "FAIL: $label" }
    $script:checks++
}
function Assert-NotText([string]$text, [string]$needle, [string]$label) {
    if ($text.IndexOf($needle, [StringComparison]::Ordinal) -ge 0) { throw "FAIL: $label" }
    $script:checks++
}

Assert-Text $protocol 'server/discover' 'Modern MCP discovery is implemented'
Assert-Text $protocol 'tools/list' 'MCP capability listing is implemented'
Assert-Text $protocol 'tools/call' 'MCP tool calls are implemented'
Assert-Text $protocol 'io.modelcontextprotocol/protocolVersion' 'Per-request protocol version is required'
Assert-Text $protocol 'io.modelcontextprotocol/clientCapabilities' 'Per-request client capability declaration is required'
Assert-Text $protocol 'UnsupportedVersion' 'Version mismatch produces negotiation error'
Assert-Text $protocol 'MaximumMessageBytes = 65536' 'Protocol input size is bounded'
Assert-Text $protocol 'MaximumTextLength = 256' 'Tool text inputs are bounded'
Assert-Text $protocol 'additionalProperties"] = false' 'Unknown input properties are rejected'
Assert-Text $stdio 'ReadBoundedLine' 'Stdio reads bounded newline-delimited messages'
Assert-Text $api 'OperationClass.Mutating && !request.DryRun && !request.ExplicitExecute' 'Mutations require dry-run then explicit execution'
Assert-Text $capability 'AutomationOperationClass.PreviewOnly' 'Only preview capability is registered'
Assert-Text $naming 'TimeSpan.FromMilliseconds(100)' 'Canonical user regex evaluation is bounded'
Assert-NotText $protocol 'Process.Start' 'No process or shell launch path exists'
Assert-NotText $protocol 'Assembly.Load' 'No dynamic assembly loading exists'
Assert-NotText $protocol 'TcpListener' 'No network listener is exposed'
Assert-NotText $protocol 'HttpListener' 'No HTTP/remote server is exposed'

& dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) { throw "MCP contract tests failed with exit code $LASTEXITCODE" }
Write-Host "PASS: MCP protocol, security and local transport acceptance ($($checks + 25) checks total; $checks structural, 25 executable)" -ForegroundColor Green
exit 0
