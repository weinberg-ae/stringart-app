@echo off
chcp 65001 >nul
cd /d "%~dp0"
python -m pip install --user --upgrade edge-tts >nul
python make_voice_test.py
start "" "%~dp0voice_test"
pause
