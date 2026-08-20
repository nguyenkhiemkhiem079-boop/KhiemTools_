# -*- coding: utf-8 -*-
"""
KhiemTools Auto-Updater Module
Author: Khim
Repository: nguyenkhiemkhiem079-boop/KhiemTools_
"""

import os
import sys
import requests
from packaging import version

# Phiên bản hiện tại của ứng dụng
__version__ = "1.0.0"

REPO_OWNER = "nguyenkhiemkhiem079-boop"
REPO_NAME = "KhiemTools_"
GITHUB_API_LATEST = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest"
GITHUB_RAW_BASE = f"https://raw.githubusercontent.com/{REPO_OWNER}/{REPO_NAME}/main"


def get_resource_path(relative_path: str) -> str:
    """
    Lấy đường dẫn tuyệt đối đến tài nguyên (assets, icons, configs).
    Tương thích cả lúc chạy mã nguồn Python trực tiếp và lúc chạy từ file PyInstaller .exe (_MEIPASS).
    """
    if hasattr(sys, "_MEIPASS"):
        # Chạy trong môi trường PyInstaller bundle
        base_path = sys._MEIPASS
    else:
        # Chạy file .py thông thường
        base_path = os.path.abspath(os.path.dirname(__file__))
    return os.path.join(base_path, relative_path)


def check_for_updates(current_ver: str = __version__, timeout: int = 5) -> dict:
    """
    Kiểm tra GitHub Releases API để tìm phiên bản mới.
    Trả về dict: {
        'has_update': bool,
        'latest_version': str,
        'release_name': str,
        'body': str,
        'html_url': str,
        'download_url': str
    }
    """
    result = {
        "has_update": False,
        "latest_version": current_ver,
        "release_name": "",
        "body": "",
        "html_url": "",
        "download_url": "",
        "error": None,
    }

    try:
        headers = {"Accept": "application/vnd.github.v3+json"}
        resp = requests.get(GITHUB_API_LATEST, headers=headers, timeout=timeout)
        
        if resp.status_code == 200:
            data = resp.json()
            tag_name = data.get("tag_name", "").lstrip("v")
            
            # So sánh version theo chuẩn SemVer
            if tag_name and version.parse(tag_name) > version.parse(current_ver):
                result["has_update"] = True
                result["latest_version"] = tag_name
                result["release_name"] = data.get("name", "")
                result["body"] = data.get("body", "")
                result["html_url"] = data.get("html_url", "")
                
                # Tìm asset file .exe hoặc .zip trong release
                assets = data.get("assets", [])
                for asset in assets:
                    name = asset.get("name", "").lower()
                    if name.endswith(".exe") or name.endswith(".zip"):
                        result["download_url"] = asset.get("browser_download_url", "")
                        break
        elif resp.status_code == 404:
            result["error"] = "Chưa có Release nào trên GitHub."
        else:
            result["error"] = f"GitHub API trả về mã lỗi: {resp.status_code}"

    except requests.RequestException as e:
        result["error"] = f"Lỗi kết nối mạng: {str(e)}"
    except Exception as e:
        result["error"] = f"Lỗi kiểm tra phiên bản: {str(e)}"

    return result


def fetch_raw_config(relative_path: str, timeout: int = 5) -> str:
    """
    Tải nội dung file cấu hình động trực tiếp từ nhánh main qua GitHub Raw URL.
    Ví dụ: fetch_raw_config("update_info.json")
    """
    url = f"{GITHUB_RAW_BASE}/{relative_path.lstrip('/')}"
    try:
        resp = requests.get(url, timeout=timeout)
        if resp.status_code == 200:
            return resp.text
    except Exception:
        pass
    return ""


def download_file(url: str, output_path: str, progress_callback=None) -> bool:
    """
    Tải file từ URL về máy với hỗ trợ callback tiến trình (progress).
    """
    try:
        with requests.get(url, stream=True, timeout=30) as r:
            r.raise_for_status()
            total_size = int(r.headers.get("content-length", 0))
            downloaded = 0
            
            with open(output_path, "wb") as f:
                for chunk in r.iter_content(chunk_size=8192):
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)
                        if progress_callback and total_size > 0:
                            progress_callback(downloaded / total_size * 100)
            return True
    except Exception:
        return False
