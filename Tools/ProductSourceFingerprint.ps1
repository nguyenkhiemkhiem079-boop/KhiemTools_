function Get-KToolProductSourceFingerprint([string]$RepositoryRoot) {
    $sourceRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd('\','/')
    $inputs = @(Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'KhimTools'),(Join-Path $sourceRoot 'src'),(Join-Path $sourceRoot 'Installer/K-TOOLS.MSI'),(Join-Path $sourceRoot 'Installer/K-TOOLS.Bootstrapper') -Recurse -File |
        Where-Object { $_.Extension -in @('.cs','.xaml','.csproj','.wxs','.wixproj','.xml','.config') -and $_.FullName -notmatch '[\\/](bin|obj|Output|Tests|App|IconGen)[\\/]' } |
        Sort-Object FullName)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $tokens = foreach ($inputFile in $inputs) {
            $relative = $inputFile.FullName.Substring($sourceRoot.Length).TrimStart('\','/').Replace('\','/')
            $normalized = [IO.File]::ReadAllText($inputFile.FullName).Replace("`r`n","`n")
            $contentHash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($normalized))).Replace('-','').ToLowerInvariant()
            $relative + "`t" + $contentHash
        }
        return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($tokens -join "`n")))).Replace('-','').ToLowerInvariant()
    } finally { $sha.Dispose() }
}
