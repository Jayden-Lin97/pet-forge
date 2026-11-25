import argparse
from pathlib import Path
import webbrowser
from .server import create_app


def main():
    parser = argparse.ArgumentParser(description="PetForge — 照片、视频变成电脑桌宠")
    parser.add_argument("command",choices=["serve"],nargs="?",default="serve")
    parser.add_argument("--port",type=int,default=8108)
    parser.add_argument("--work",type=Path,default=Path.cwd()/".work")
    parser.add_argument("--no-browser",action="store_true")
    args = parser.parse_args()
    app = create_app(args.work)
    if not args.no_browser:
        webbrowser.open(f"http://127.0.0.1:{args.port}/")
    print(f"PetForge: http://127.0.0.1:{args.port}/")
    app.run(host="127.0.0.1",port=args.port,debug=False,threaded=True,use_reloader=False)


if __name__ == "__main__":
    main()
