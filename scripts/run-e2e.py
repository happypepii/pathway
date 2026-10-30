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
for port in (5173, 7040):
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
        plan = {"milestones": [{"title": "起步", "tasks": [
            {"title": "準備材料", "estimatedHours": 1},
            {"title": "實作練習", "estimatedHours": 1},
            {"title": "閱讀進階資料", "estimatedHours": 1},
        ]}]}
        body = json.dumps({"response": json.dumps(plan, ensure_ascii=False)}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

def http_journey(tls):
    base = "https://localhost:5173"
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
    upstream = ThreadingHTTPServer(("127.0.0.1", 0), Fixture)
    threading.Thread(target=upstream.serve_forever, daemon=True).start()
    env = os.environ.copy()
    env.update({
        "ASPNETCORE_ENVIRONMENT": "Production",
        "ASPNETCORE_Kestrel__Certificates__Default__Path": cert,
        "ASPNETCORE_Kestrel__Certificates__Default__KeyPath": key,
        "PATHWAY_DEV_CERT": cert, "PATHWAY_DEV_KEY": key,
        "Planning__Endpoint": f"http://127.0.0.1:{upstream.server_port}/api/generate",
        "Planning__Model": "test-fixture-not-live-model",
    })
    processes = []
    log = open(temporary + "/services.log", "w")
    try:
        processes.append(subprocess.Popen([
            "dotnet", "run", "--project", "backend/Pathway.Api",
            "--no-build", "--no-launch-profile", "--urls", "https://localhost:7040",
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
                with urllib.request.urlopen("https://localhost:5173/api/v1/antiforgery", context=tls, timeout=1) as response:
                    assert response.status == 200
                break
            except Exception:
                if attempt == 119:
                    raise
                time.sleep(.25)
        http_journey(tls)
        if os.environ.get("PATHWAY_E2E_HTTP_ONLY") != "1":
            subprocess.run(["frontend/node_modules/.bin/playwright", "test",
                            "--config", "frontend/playwright.config.ts"], env=env, check=True)
    finally:
        for process in reversed(processes):
            process.terminate()
        for process in processes:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        upstream.shutdown()
        upstream.server_close()
        log.close()
