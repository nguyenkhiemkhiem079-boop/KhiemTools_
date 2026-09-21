const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const { icons } = require('lucide');

const output = path.resolve(__dirname, '..', 'KhimTools', 'Resources');

const palette = {
  general: '#263238',
  structural: '#263238',
  architectural: '#263238',
  mep: '#263238',
  utility: '#526170',
  danger: '#D32F2F',
  accent: '#0097A7'
};

const specs = {
  column_rebar: ['Columns3', 'structural'],
  export_sheet: ['FileSpreadsheet', 'general'],
  icon_align: ['AlignCenter', 'general'],
  icon_callout_pro: ['ScanSearch', 'general'],
  icon_copylink: { kind: 'custom', draw: drawCopyLink },
  icon_cover_setup: { kind: 'custom', draw: drawCover },
  icon_detail: ['ListChecks', 'general'],
  icon_export: ['FileUp', 'general'],
  icon_finishes: ['PaintRoller', 'architectural'],
  icon_grid: ['Grid3X3', 'general'],
  icon_grid_plan: { kind: 'custom', draw: drawGridPlan },
  icon_join: { kind: 'custom', draw: drawJoin },
  icon_mep_openings: { kind: 'custom', draw: drawMepOpening },
  icon_mep_tags: { kind: 'custom', draw: drawElevationTag },
  icon_room3d: { kind: 'custom', draw: drawRoom3d },
  icon_section_cut: { kind: 'custom', draw: drawSectionCut },
  icon_sectionbox: ['Scan', 'general'],
  icon_unjoin: ['Unlink2', 'general'],
  icon_update: ['RefreshCw', 'general'],
  icon_view_callout: ['SquareArrowOutUpRight', 'general'],
  icon_workspace: { kind: 'custom', draw: drawWorkspace },
  icon_family: { kind: 'custom', draw: drawFamily },
  icon_slabstep: { kind: 'custom', draw: drawSlabStep },
  icon_language: ['Languages', 'utility'],
  icon_quantity: { kind: 'custom', draw: drawQuantity },
  icon_data_check: { kind: 'custom', draw: drawDataCheck },
  icon_search: ['Search', 'utility'],
  icon_star: ['Star', 'general'],
  override_blue: ['Circle', '#2563EB'],
  override_custom: ['Pipette', 'utility'],
  override_cyan: ['Circle', '#06B6D4'],
  override_gray: ['Circle', '#64748B'],
  override_green: ['Circle', '#16A34A'],
  override_halftone: ['Blend', 'utility'],
  override_magenta: ['Circle', '#DB2777'],
  override_orange: ['Circle', '#EA580C'],
  override_palette: { kind: 'custom', draw: drawOverride },
  override_red: ['Circle', '#DC2626'],
  override_reset: ['RotateCcw', 'utility'],
  override_setting: ['SlidersHorizontal', 'utility'],
  override_yellow: ['Circle', '#CA8A04'],
  rebar_beam: { kind: 'custom', draw: drawBeamRebar },
  rebar_col: { kind: 'custom', draw: drawColumnRebar },
  rebar_col_circ: { kind: 'custom', draw: drawCircularRebar },
  rebar_col_rect: { kind: 'custom', draw: drawColumnRebar },
  rebar_cover: ['Shield', 'structural'],
  rebar_draw: ['DraftingCompass', 'structural'],
  rebar_fdn: ['PanelBottom', 'structural'],
  rebar_qa: ['BadgeCheck', 'structural'],
  rebar_slab: { kind: 'custom', draw: drawSlabRebar }
};

function svgShell(size, body) {
  const sw = size === 16 ? 2.1 : 1.8;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 32 32" fill="none" stroke="#263238" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round">${body}</svg>`;
}
const L = (x1, y1, x2, y2, stroke = '#263238') => `<path d="M${x1} ${y1}L${x2} ${y2}" stroke="${stroke}"/>`;
const R = (x, y, w, h, stroke = '#263238') => `<rect x="${x}" y="${y}" width="${w}" height="${h}" stroke="${stroke}"/>`;
const C = (cx, cy, r, fill = 'none', stroke = '#0097A7') => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${fill}" stroke="${stroke}"/>`;
function drawWorkspace(size) { return svgShell(size, `${R(4,5,24,21)}${R(8,9,13,17)}${R(22,9,6,17,'#0097A7')}`); }
function drawCopyLink(size) { return svgShell(size, `${R(4,7,14,17)}${R(14,10,14,17)}${L(10,4,22,4,'#0097A7')}${L(19,1,22,4,'#0097A7')}${L(19,7,22,4,'#0097A7')}`); }
function drawJoin(size) { return svgShell(size, `${R(4,9,15,12)}${R(13,9,15,12)}<path d="M13 12h6v6h-6z" fill="#0097A7" stroke="#0097A7"/>`); }
function drawSlabStep(size) { return svgShell(size, `${L(3,10,18,10)}${L(18,10,18,21)}${L(18,21,29,21)}${L(3,25,29,25)}${L(18,10,18,21,'#0097A7')}`); }
function drawGridPlan(size) { return svgShell(size, `${R(5,5,21,21)}${L(12,5,12,26)}${L(19,5,19,26)}${L(5,12,26,12)}${L(5,19,26,19)}${R(22,22,7,7,'#0097A7')}`); }
function drawFamily(size) { return svgShell(size, `${R(5,8,18,18)}${L(5,8,14,4)}${L(23,8,14,4)}${L(14,4,14,12)}${L(14,12,23,8)}${R(10,13,8,8,'#0097A7')}`); }
function drawMepOpening(size) { return svgShell(size, `${L(3,9,29,9)}${L(3,23,29,23)}${C(16,16,5,'none','#0097A7')}${L(11,16,21,16,'#0097A7')}`); }
function drawElevationTag(size) { return svgShell(size, `${L(5,9,25,9)}${L(16,9,16,25)}<path d="M11 25h10l-5 4z" fill="#0097A7" stroke="#0097A7"/>`); }
function drawRoom3d(size) { return svgShell(size, `${L(5,11,16,5)}${L(16,5,27,11)}${L(5,11,16,17)}${L(27,11,16,17)}${L(5,11,5,23)}${L(16,17,16,29)}${L(27,11,27,23)}${L(5,23,16,29)}${L(27,23,16,29)}<path d="M10 14l6 3v7l-6-3z" fill="#0097A7" stroke="#0097A7"/>`); }
function drawSectionCut(size) { return svgShell(size, `${R(7,7,18,18)}${L(3,26,29,6,'#0097A7')}${L(3,26,8,25,'#0097A7')}${L(3,26,5,21,'#0097A7')}${L(29,6,24,7,'#0097A7')}${L(29,6,27,11,'#0097A7')}`); }
function drawOverride(size) { return svgShell(size, `${R(6,6,20,20)}${L(6,20,18,8,'#0097A7')}${L(18,8,26,16,'#0097A7')}${L(18,8,26,8,'#0097A7')}`); }
function drawCover(size) { return svgShell(size, `${R(5,5,22,22)}${C(13,16,3,'#0097A7')}${L(16,16,25,16,'#0097A7')}${L(23,14,25,16,'#0097A7')}${L(23,18,25,16,'#0097A7')}`); }
function drawColumnRebar(size) { return svgShell(size, `${R(6,6,20,20)}${C(11,11,1.7,'#0097A7')}${C(21,11,1.7,'#0097A7')}${C(11,21,1.7,'#0097A7')}${C(21,21,1.7,'#0097A7')}`); }
function drawCircularRebar(size) { return svgShell(size, `${C(16,16,11)}${C(16,16,7)}${C(16,7,1.7,'#0097A7')}${C(24,12,1.7,'#0097A7')}${C(21,23,1.7,'#0097A7')}${C(11,23,1.7,'#0097A7')}${C(8,12,1.7,'#0097A7')}`); }
function drawBeamRebar(size) { return svgShell(size, `${R(3,9,26,14)}${C(7,13,1.7,'#0097A7')}${C(25,13,1.7,'#0097A7')}${C(7,19,1.7,'#0097A7')}${C(25,19,1.7,'#0097A7')}${R(6,11,20,10,'#0097A7')}`); }
function drawSlabRebar(size) { return svgShell(size, `${R(4,6,24,20)}${L(4,11,28,11,'#0097A7')}${L(4,16,28,16,'#0097A7')}${L(4,21,28,21,'#0097A7')}${L(10,6,10,26,'#0097A7')}${L(16,6,16,26,'#0097A7')}${L(22,6,22,26,'#0097A7')}`); }
function drawQuantity(size) { return svgShell(size, `${R(5,5,22,22)}${L(9,11,23,11)}${L(9,16,23,16)}${L(9,21,19,21)}${L(6,11,7,11,'#0097A7')}${L(6,16,7,16,'#0097A7')}${L(6,21,7,21,'#0097A7')}`); }
function drawDataCheck(size) { return svgShell(size, `${R(5,5,22,22)}${L(9,11,13,15,'#2E7D32')}${L(13,15,22,8,'#2E7D32')}${L(9,21,23,21)}`); }

function esc(value) {
  return String(value).replace(/&/g, '&amp;').replace(/"/g, '&quot;');
}

function nodeToSvg([tag, attrs]) {
  const body = Object.entries(attrs).map(([key, value]) => `${key}="${esc(value)}"`).join(' ');
  return `<${tag} ${body}/>`;
}

function svgFor(iconName, color, size) {
  const icon = icons[iconName];
  if (!icon) throw new Error(`Unknown Lucide icon: ${iconName}`);
  const resolved = palette[color] || color;
  const strokeWidth = size === 16 ? 2.1 : 1.85;
  const mark = icon.map(nodeToSvg).join('');
  if (iconName === 'Circle') {
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 24 24"><rect x="3" y="3" width="18" height="18" fill="${resolved}" stroke="#526170" stroke-width="0.75"/></svg>`;
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="${resolved}" stroke-width="${strokeWidth}" stroke-linecap="round" stroke-linejoin="round">${mark}</svg>`;
}

function svgForSpec(spec, size) {
  if (spec.kind === 'custom') return spec.draw(size);
  return svgFor(spec[0], spec[1], size);
}

async function main() {
  const sourceRoot = path.resolve(__dirname, '..', 'KhimTools');
  const referenced = new Set();

  function scan(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      if (entry.name === 'bin' || entry.name === 'obj' || entry.name === 'Resources') continue;
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) scan(fullPath);
      else if (/\.(cs|xaml|csproj)$/.test(entry.name)) {
        const source = fs.readFileSync(fullPath, 'utf8');
        for (const match of source.matchAll(/[-A-Za-z0-9_]+_(16|32)\.png/g)) referenced.add(match[0]);
      }
    }
  }

  scan(sourceRoot);
  // Validate the entire catalog before writing. Never delete unrelated images.
  for (const file of referenced) {
    if (!specs[file.replace(/_(16|32)\.png$/, '')]) throw new Error(`Missing icon specification: ${file}`);
  }
  for (const [name, spec] of Object.entries(specs)) {
    if (spec.kind !== 'custom' && !icons[spec[0]]) throw new Error(`Unknown icon for ${name}: ${spec[0]}`);
    referenced.add(`${name}_16.png`);
    referenced.add(`${name}_32.png`);
  }
  fs.mkdirSync(output, { recursive: true });
  for (const file of [...referenced].sort()) {
    const match = file.match(/^(.+)_(16|32)\.png$/);
    const name = match[1];
    const size = Number(match[2]);
    if (!specs[name]) throw new Error(`No icon specification for referenced asset: ${file}`);
    await sharp(Buffer.from(svgForSpec(specs[name], size))).resize(size, size).png({ compressionLevel: 9 }).toFile(path.join(output, file));
  }

  console.log(`Generated ${referenced.size} icons in ${output}`);
  if (process.argv.includes('--preview')) {
    const names = Object.keys(specs);
    const width = 1200;
    const cellWidth = 150;
    const cellHeight = 86;
    const layers = [];
    for (const [index, name] of names.entries()) {
      const left = (index % 8) * cellWidth;
      const top = Math.floor(index / 8) * cellHeight;
      layers.push({ input: path.join(output, `${name}_32.png`), left: left + 40, top: top + 12 });
      layers.push({ input: path.join(output, `${name}_16.png`), left: left + 85, top: top + 20 });
      const label = name.replace(/^icon_/, '').replace(/_/g, ' ');
      const svg = `<svg width="150" height="24"><text x="75" y="16" text-anchor="middle" font-family="Segoe UI" font-size="11" fill="#526170">${esc(label)}</text></svg>`;
      layers.push({ input: Buffer.from(svg), left, top: top + 52 });
    }
    const previewDir = path.resolve(__dirname, '..', 'artifacts', 'ui-qa');
    fs.mkdirSync(previewDir, { recursive: true });
    const previewHeight = Math.ceil(names.length / 8) * cellHeight;
    const labelLayers = layers;
    await sharp({ create: { width, height: previewHeight, channels: 4, background: '#F5F7FA' } })
      .composite(labelLayers).png().toFile(path.join(previewDir, 'ribbon-icons-light.png'));
    const darkLabels = names.flatMap((name, index) => {
      const left = (index % 8) * cellWidth;
      const top = Math.floor(index / 8) * cellHeight;
      const label = name.replace(/^icon_/, '').replace(/_/g, ' ');
      const svg = `<svg width="150" height="24"><text x="75" y="16" text-anchor="middle" font-family="Segoe UI" font-size="11" fill="#D8E1E5">${esc(label)}</text></svg>`;
      return [{ input: path.join(output, `${name}_32.png`), left: left + 40, top: top + 12 }, { input: path.join(output, `${name}_16.png`), left: left + 85, top: top + 20 }, { input: Buffer.from(svg), left, top: top + 52 }];
    });
    await sharp({ create: { width, height: previewHeight, channels: 4, background: '#263238' } })
      .composite(darkLabels).png().toFile(path.join(previewDir, 'ribbon-icons-dark.png'));
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
