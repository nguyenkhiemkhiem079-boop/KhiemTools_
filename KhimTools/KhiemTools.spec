# -*- mode: python ; coding: utf-8 -*-

import os
import sys

block_cipher = None

# Danh sách file tĩnh/tài nguyên đính kèm (icon, assets, templates)
added_datas = []

if os.path.exists('Resources'):
    added_datas.append(('Resources', 'Resources'))
if os.path.exists('assets'):
    added_datas.append(('assets', 'assets'))

a = Analysis(
    ['updater.py'],
    pathex=[],
    binaries=[],
    datas=added_datas,
    hiddenimports=[
        'requests',
        'packaging',
        'packaging.version',
        'json',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['matplotlib', 'scipy', 'numpy', 'pytest', 'tkinter'],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name='KhiemTools',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon='Resources/export_sheet_32.png' if os.path.exists('Resources/export_sheet_32.png') else None,
)
