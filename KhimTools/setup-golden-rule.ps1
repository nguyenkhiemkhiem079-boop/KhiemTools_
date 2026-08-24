# ============================================================
#  Golden Rule setup - chay 1 lan tren MOI may (cong ty + nha)
#  Mo PowerShell trong thu muc goc cua repo (KhimTools) roi chay:
#     .\setup-golden-rule.ps1
# ============================================================

Write-Host "== 1. Chuan hoa line endings: de .gitattributes quyet dinh ==" -ForegroundColor Cyan
git config --global core.autocrlf false

Write-Host "== 2. Pull mac dinh luon dung --rebase (duong thang, khong merge-commit rac) ==" -ForegroundColor Cyan
git config --global pull.rebase true
git config --global rebase.autostash true   # tu stash/pop neu dang co sua do khi rebase

Write-Host "== 3. Alias cho Golden Rule 2 buoc ==" -ForegroundColor Cyan

# Bat dau buoi lam viec: lay code moi nhat theo nhanh hien tai
git config --global alias.start "pull --rebase"

# Ket thuc buoi lam viec: add + commit + push
#   Dung: git save "feat: mo ta ngan gon"
git config --global alias.save '!f() { git add -A && git commit -m "$1" && git push; }; f'

# Neu code CHUA build duoc / do dang -> luu tam ma KHONG day len master
#   Dung: git wip
git config --global alias.wip "!git add -A && git commit -m 'WIP: chua build xong' --no-verify"

# Xem nhanh tinh trang truoc khi tat may: co gi chua commit / chua push khong
git config --global alias.checkout-status "!git status -sb && echo [Unpushed Commits] && git log @{u}.. --oneline"

Write-Host "== 4. Kiem tra .gitattributes trong repo ==" -ForegroundColor Cyan
if (-not (Test-Path ".gitattributes")) {
    Write-Host "  -> Chua thay .gitattributes trong repo nay." -ForegroundColor Yellow
} else {
    Write-Host "  -> Da co .gitattributes, OK." -ForegroundColor Green
}

Write-Host ""
Write-Host "XONG! Tu gio quy trinh moi buoi lam viec:" -ForegroundColor Green
Write-Host "  Bat dau                                       : git start"
Write-Host "  Code xong, build sach, san sang chia se       : git save 'feat: mo ta ngan gon'"
Write-Host "  Sap tat may nhung code CHUA build duoc        : git wip"
Write-Host "  Truoc khi tat may, kiem tra con sot gi khong  : git checkout-status"
