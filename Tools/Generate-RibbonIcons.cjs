const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const { icons } = require('lucide');

const output = path.resolve(__dirname, '..', 'KhimTools', 'Resources');

const palette = {
  general: '#1677D2',
  structural: '#1677D2',
  architectural: '#1677D2',
  mep: '#1677D2',
  utility: '#526170',
  danger: '#DC2626'
};

const specs = {
  column_rebar: ['Columns3', 'structural'],
  export_sheet: ['FileSpreadsheet', 'general'],
  icon_align: ['AlignCenter', 'general'],
  icon_callout_pro: ['ScanSearch', 'general'],
  icon_copylink: ['CopyPlus', 'general'],
  icon_cover_setup: ['ShieldCheck', 'structural'],
  icon_detail: ['ListChecks', 'general'],
  icon_export: ['FileUp', 'general'],
  icon_finishes: ['PaintRoller', 'architectural'],
  icon_grid: ['Grid3X3', 'general'],
  icon_grid_plan: ['Grid2X2Plus', 'general'],
  icon_join: ['Combine', 'general'],
  icon_mep_openings: ['BetweenHorizontalStart', 'mep'],
  icon_mep_tags: ['Tag', 'mep'],
  icon_room3d: ['Box', 'architectural'],
  icon_section_cut: ['Slice', 'structural'],
  icon_sectionbox: ['Scan', 'general'],
  icon_unjoin: ['Unlink2', 'general'],
  icon_update: ['RefreshCw', 'general'],
  icon_view_callout: ['SquareArrowOutUpRight', 'general'],
  icon_workspace: ['PanelsTopLeft', 'general'],
  icon_family: ['LibraryBig', 'general'],
  icon_slabstep: ['Layers2', 'general'],
  icon_language: ['Languages', 'utility'],
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
  override_palette: ['Palette', 'general'],
  override_red: ['Circle', '#DC2626'],
  override_reset: ['RotateCcw', 'utility'],
  override_setting: ['SlidersHorizontal', 'utility'],
  override_yellow: ['Circle', '#CA8A04'],
  rebar_beam: ['BetweenHorizontalEnd', 'structural'],
  rebar_col: ['PanelTop', 'structural'],
  rebar_col_circ: ['CircleDot', 'structural'],
  rebar_col_rect: ['SquareDot', 'structural'],
  rebar_cover: ['Shield', 'structural'],
  rebar_draw: ['DraftingCompass', 'structural'],
  rebar_fdn: ['PanelBottom', 'structural'],
  rebar_qa: ['BadgeCheck', 'structural'],
  rebar_slab: ['Layers3', 'structural']
};

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
  for (const [name, [icon]] of Object.entries(specs)) {
    if (!icons[icon]) throw new Error(`Unknown icon for ${name}: ${icon}`);
    referenced.add(`${name}_16.png`);
    referenced.add(`${name}_32.png`);
  }
  fs.mkdirSync(output, { recursive: true });
  for (const file of [...referenced].sort()) {
    const match = file.match(/^(.+)_(16|32)\.png$/);
    const name = match[1];
    const size = Number(match[2]);
    if (!specs[name]) throw new Error(`No icon specification for referenced asset: ${file}`);
    const [iconName, color] = specs[name];
    await sharp(Buffer.from(svgFor(iconName, color, size))).resize(size, size).png({ compressionLevel: 9 }).toFile(path.join(output, file));
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
    await sharp({ create: { width, height: Math.ceil(names.length / 8) * cellHeight, channels: 4, background: '#F5F7FA' } })
      .composite(layers).png().toFile(path.join(previewDir, 'ribbon-icons.png'));
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
