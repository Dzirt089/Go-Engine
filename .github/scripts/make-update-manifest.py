#!/usr/bin/env python3
"""Собирает update.json — манифест обновления для приложения.

Манифест это публичный контракт: приложение читает его по постоянному адресу
releases/download/latest/update.json и по нему решает, нужно ли обновление.
Ссылки указывают на версионированный тег, поэтому скачивание не «переедет» на другую сборку.
Запуск: VERSION=... TAG=... REPO=... python3 make-update-manifest.py dist
"""
import hashlib
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

# Ключ платформы (его знает приложение) → чем узнаётся файл. Первое совпадение и берётся,
# поэтому подписанный APK имеет приоритет над неподписанным.
PLATFORMS = {
    "win-x64": ["GoEngine-Setup.exe"],
    "android": ["-Signed.apk", ".apk"],
    "linux-x64": ["go-engine-linux-x64.tar.gz"],
    "osx-x64": ["go-engine-osx-x64.tar.gz"],
    "osx-arm64": ["go-engine-osx-arm64.tar.gz"],
}


def pick(files, patterns):
    """Возвращает первый файл, подходящий под шаблоны по приоритету."""
    for pattern in patterns:
        matches = sorted((f for f in files if f.name.endswith(pattern)), key=lambda p: p.name)
        if matches:
            return matches[0]

    return None


def main():
    dist = Path(sys.argv[1] if len(sys.argv) > 1 else "dist")
    version = os.environ["VERSION"]
    tag = os.environ["TAG"]
    repo = os.environ["REPO"]

    files = [p for p in dist.rglob("*") if p.is_file() and p.name != "update.json"]
    assets = {}

    for key, patterns in PLATFORMS.items():
        chosen = pick(files, patterns)

        if chosen is None:
            print(f"::warning::в выпуске нет файла для платформы {key}")
            continue

        digest = hashlib.sha256(chosen.read_bytes()).hexdigest().upper()
        assets[key] = {
            "name": chosen.name,
            "url": f"https://github.com/{repo}/releases/download/{tag}/{chosen.name}",
            "size": chosen.stat().st_size,
            "sha256": digest,
        }
        print(f"{key}: {chosen.name} — {chosen.stat().st_size} байт, SHA-256 {digest[:16]}…")

    if not assets:
        raise SystemExit("в выпуске нет ни одного файла для манифеста обновления")

    manifest = {
        "version": version,
        "tag": tag,
        "publishedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "commit": os.environ.get("COMMIT", ""),
        "assets": assets,
    }

    target = dist / "update.json"
    target.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"манифест обновления: {target}, платформ {len(assets)}, размер {target.stat().st_size} байт")


if __name__ == "__main__":
    main()
