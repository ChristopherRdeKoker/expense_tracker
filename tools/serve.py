import argparse
import http.server
import mimetypes
import os
import posixpath
import sys
import urllib.parse

CSP = (
    "default-src 'none'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; "
    "img-src 'self' data:; connect-src 'self'; font-src 'self'; object-src 'none'; "
    "base-uri 'none'; form-action 'none'"
)

TYPES = {
    ".html": "text/html; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".mjs": "text/javascript; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".json": "application/json",
    ".wasm": "application/wasm",
    ".dll": "application/octet-stream",
    ".dat": "application/octet-stream",
    ".blat": "application/octet-stream",
    ".pdb": "application/octet-stream",
    ".svg": "image/svg+xml",
    ".png": "image/png",
    ".ico": "image/x-icon",
    ".txt": "text/plain; charset=utf-8",
}


def make_handler(root, prefix, csp):
    class Handler(http.server.BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, fmt, *args):
            sys.stderr.write("%s %s\n" % (self.command, fmt % args))

        def send_plain(self, code, text, extra=None):
            body = text.encode()
            self.send_response(code)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Content-Security-Policy", csp)
            self.send_header("X-Content-Type-Options", "nosniff")
            for k, v in (extra or {}).items():
                self.send_header(k, v)
            self.end_headers()
            if self.command != "HEAD":
                self.wfile.write(body)

        def do_HEAD(self):
            self.do_GET()

        def do_GET(self):
            parsed = urllib.parse.urlsplit(self.path)
            path = urllib.parse.unquote(parsed.path)
            if path == prefix.rstrip("/"):
                target = prefix + ("?" + parsed.query if parsed.query else "")
                self.send_plain(301, "Moved\n", {"Location": target})
                return
            if not path.startswith(prefix):
                self.send_plain(404, "Not found\n")
                return
            rest = path[len(prefix):]
            if rest == "" or rest.endswith("/"):
                rest += "index.html"
            rel = posixpath.normpath(rest).lstrip("/")
            if rel.startswith(".."):
                self.send_plain(404, "Not found\n")
                return
            full = os.path.join(root, rel)
            if not os.path.isfile(full):
                self.send_plain(404, "Not found\n")
                return
            ext = os.path.splitext(full)[1].lower()
            ctype = TYPES.get(ext) or mimetypes.guess_type(full)[0] or "application/octet-stream"
            encoding = None
            accept = self.headers.get("Accept-Encoding", "")
            if "br" in accept and os.path.isfile(full + ".br"):
                full, encoding = full + ".br", "br"
            elif "gzip" in accept and os.path.isfile(full + ".gz"):
                full, encoding = full + ".gz", "gzip"
            with open(full, "rb") as f:
                body = f.read()
            self.send_response(200)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Content-Security-Policy", csp)
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Cache-Control", "no-cache")
            if encoding:
                self.send_header("Content-Encoding", encoding)
                self.send_header("Vary", "Accept-Encoding")
            self.end_headers()
            if self.command != "HEAD":
                self.wfile.write(body)

    return Handler


def main():
    parser = argparse.ArgumentParser(description="Serve the published app under a path prefix with the production CSP.")
    parser.add_argument("root", help="published wwwroot folder")
    parser.add_argument("--port", type=int, default=8733)
    parser.add_argument("--prefix", default="/apps/expenses/")
    parser.add_argument("--csp", default=CSP)
    args = parser.parse_args()
    prefix = "/" + args.prefix.strip("/") + "/"
    server = http.server.ThreadingHTTPServer(("127.0.0.1", args.port), make_handler(os.path.abspath(args.root), prefix, args.csp))
    print(f"Serving {args.root} at http://127.0.0.1:{args.port}{prefix}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
