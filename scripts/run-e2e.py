"""Test-only protocol fixture, ephemeral TLS and real PostgreSQL browser harness.
Not a production provider or evidence of live-model quality.
Invoke through test-e2e.sh so the database is always disposable.
"""
import json
import http.cookiejar
from datetime import datetime, timedelta, timezone
import os
from pathlib import Path
import socket
import ssl
import subprocess
import tempfile
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = Path(__file__).resolve().parent.parent
os.chdir(ROOT)
LIVE = os.environ.get("PATHWAY_E2E_LIVE") == "1"
FRONTEND_PORT = int(os.environ.get("PATHWAY_E2E_FRONTEND_PORT", "5173"))
BACKEND_PORT = int(os.environ.get("PATHWAY_E2E_BACKEND_PORT", "7040"))
BASE_URL = f"https://localhost:{FRONTEND_PORT}"
for port in (FRONTEND_PORT, BACKEND_PORT):
    if not 1 <= port <= 65535:
        raise ValueError("Invalid test service port")
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", port))

class Fixture(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_POST(self):
        payload = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        assert payload["stream"] is False
        assert payload["format"]["type"] == "object"
        if "__PROVIDER_FAIL__" in payload["prompt"]:
            self.send_response(503)
            self.end_headers()
            return
        plan = {"milestones": [{"title": "起步", "description": "建立實作基礎。", "outcome": "完成一個小成果。",
            "resources": [{"name": "入門指南", "purpose": "了解基本操作。", "searchQuery": "入門 實作 指南"}], "tasks": [
            {"title": "準備材料", "estimatedHours": 1, "description": "列出需要的材料。", "keyPoints": ["先用現有資源。"]},
            {"title": "實作練習", "estimatedHours": 1, "description": "完成第一次練習。", "keyPoints": ["記錄結果。"]},
            {"title": "閱讀進階資料", "estimatedHours": 1, "description": "閱讀適合的進階內容。", "keyPoints": ["整理筆記。"]},
        ]}]}
        body = json.dumps({"response": json.dumps(plan, ensure_ascii=False),
                           "done": True, "done_reason": "stop"}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

def http_journey(tls):
    base = BASE_URL
    def browser():
        return urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),
            urllib.request.HTTPSHandler(context=tls))
    def request(client, path, method="GET", data=None, token=None, expected=200):
        headers = {"Content-Type": "application/json"} if data is not None else {}
        if token:
            headers["X-CSRF-TOKEN"] = token
        req = urllib.request.Request(base + "/api/v1" + path,
            data=None if data is None else json.dumps(data).encode(), headers=headers, method=method)
        try:
            response = client.open(req, timeout=5)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            assert response.status == expected, (method, path, response.status, expected)
            content = response.read()
            return json.loads(content) if content else None
    def bootstrap(client):
        token = request(client, "/antiforgery")["requestToken"]
        request(client, "/sessions/anonymous", "POST", token=token, expected=201)
        return token
    owner = browser()
    csrf = bootstrap(owner)
    request(owner, "/sessions/anonymous", "POST", token=csrf)
    goal = request(owner, "/goals", "POST", {
        "title": "HTTP integration goal", "description": "Private planning context",
        "targetDate": (datetime.now(timezone.utc) + timedelta(days=30)).strftime("%Y-%m-%d"),
        "availableHoursPerWeek": 7, "timeZoneId": "UTC"
    }, csrf, 201)
    path = "/goals/" + goal["id"]
    request(owner, path + "/planning-answers", "PUT", {"experience": "Beginner"}, csrf, 204)
    preview = request(owner, path + "/plan-preview", "POST", token=csrf)
    assert request(owner, path)["milestones"] == []
    preview["milestones"][0]["tasks"].pop()
    preview["milestones"][0]["tasks"][0]["estimatedHours"] = 2
    saved = request(owner, path + "/plan", "POST", {
        "milestones": preview["milestones"], "goalVersion": preview["goalVersion"]
    }, csrf)
    tasks = saved["milestones"][0]["tasks"]
    assert len(tasks) == 2
    for task in tasks:
        request(owner, "/tasks/" + task["id"] + "/completion", "PATCH", {"completed": True}, csrf, 204)
    saved = request(owner, path)
    assert saved["status"] == "Completed" and saved["progress"] == 100
    request(owner, "/tasks/" + tasks[0]["id"] + "/completion", "PATCH", {"completed": False}, csrf, 204)
    saved = request(owner, path)
    assert saved["status"] == "Active" and saved["progress"] == 50
    other = browser()
    bootstrap(other)
    request(other, path, expected=404)
    request(owner, path + "/planning-answers", "PUT", {}, expected=400)
    request(owner, path, "DELETE", token=csrf, expected=204)
    request(owner, path, expected=404)
    print("HTTPS integration passed: session -> goal -> answers -> HTTP provider -> preview -> edited confirmation -> completion/reopen -> owner isolation -> delete")

with tempfile.TemporaryDirectory(prefix="pathway-e2e-") as temporary:
    cert = temporary + "/localhost.pem"
    key = temporary + "/localhost.key"
    subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes",
                    "-keyout", key, "-out", cert, "-days", "1", "-subj", "/CN=localhost",
                    "-addext", "subjectAltName=DNS:localhost"], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    upstream = None if LIVE else ThreadingHTTPServer(("127.0.0.1", 0), Fixture)
    if upstream is not None:
        threading.Thread(target=upstream.serve_forever, daemon=True).start()
    if LIVE and (ROOT / "frontend/secrets/localhost.pem").exists() and (ROOT / "frontend/secrets/localhost.key").exists():
        cert = str(ROOT / "frontend/secrets/localhost.pem")
        key = str(ROOT / "frontend/secrets/localhost.key")
    env = os.environ.copy()
    env.update({
        "ASPNETCORE_ENVIRONMENT": "Production",
        "ASPNETCORE_Kestrel__Certificates__Default__Path": cert,
        "ASPNETCORE_Kestrel__Certificates__Default__KeyPath": key,
        "PATHWAY_DEV_CERT": cert, "PATHWAY_DEV_KEY": key,
        "PATHWAY_DEV_PORT": str(FRONTEND_PORT), "PATHWAY_API_PORT": str(BACKEND_PORT),
        "PATHWAY_E2E_BASE_URL": BASE_URL,
        "Planning__Endpoint": "http://127.0.0.1:11434/api/generate" if LIVE else f"http://127.0.0.1:{upstream.server_port}/api/generate",
        "Planning__Model": "qwen3:4b-instruct-2507-q4_K_M" if LIVE else "test-fixture-not-live-model",
    })
    processes = []
    log = open(temporary + "/services.log", "w")
    try:
        processes.append(subprocess.Popen([
            "dotnet", "run", "--project", "backend/Pathway.Api",
            "--no-build", "--no-launch-profile", "--urls", f"https://localhost:{BACKEND_PORT}",
        ], env=env, stdout=log, stderr=log))
        processes.append(subprocess.Popen([
            "node", "frontend/node_modules/vite/bin/vite.js", "frontend",
            "--config", "frontend/vite.config.ts",
        ], env=env, stdout=log, stderr=log))
        tls = ssl.create_default_context(cafile=cert)
        for attempt in range(120):
            if any(p.poll() is not None for p in processes):
                raise RuntimeError("A test service exited before startup; check local prerequisites.")
            try:
                with urllib.request.urlopen(BASE_URL + "/api/v1/antiforgery", context=tls, timeout=1) as response:
                    assert response.status == 200
                break
            except Exception:
                if attempt == 119:
                    raise
                time.sleep(.25)
        if not LIVE:
            http_journey(tls)
        if os.environ.get("PATHWAY_E2E_HTTP_ONLY") != "1":
            subprocess.run(["frontend/node_modules/.bin/playwright", "test",
                            "--config", "frontend/playwright.config.ts"], env=env, check=True)
        if LIVE and os.environ.get("PATHWAY_E2E_KEEP_RUNNING") == "1":
            print(f"Live MVP accepted. Open {BASE_URL} (isolated test database). Ctrl+C stops this environment.", flush=True)
            while all(process.poll() is None for process in processes):
                time.sleep(1)
            raise RuntimeError("A live demo service stopped.")
    except KeyboardInterrupt:
        pass
    finally:
        for process in reversed(processes):
            process.terminate()
        for process in processes:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        if upstream is not None:
            upstream.shutdown()
            upstream.server_close()
        log.close()
