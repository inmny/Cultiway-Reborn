#!/usr/bin/env python3
"""准备、运行和回收 Cultiway 的独立开发环境，当前支持 Linux 原生游戏。"""

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import time
import uuid
from xml.sax.saxutils import escape


MOD_ENTRIES = ("Source", "Content", "GameResources", "Locales", "Assemblies", "Licenses",
               "mod.json", "icon.png", "default_config.json", "LICENSE", ".gitignore")
DEFAULT_ROOT = Path.home() / ".local/state/cultiway-dev"


class DevError(RuntimeError):
    """可以直接呈现给开发者的环境错误。"""


def stamp():
    """生成可排序且不依赖本机时区的时间标记。"""
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")


def read_json(path):
    """读取本工具管理的 JSON 文件。"""
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write_json(path, value):
    """在同一目录完整写入后替换，避免其他命令读到半份状态。"""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def command(arguments, cwd=None, env=None, timeout=120):
    """执行外部命令，保留可读的失败原因。"""
    result = subprocess.run([str(arg) for arg in arguments], cwd=cwd, env=env,
                            capture_output=True, text=True, timeout=timeout)
    if result.returncode:
        raise DevError(result.stderr.strip() or result.stdout.strip() or f"命令失败：{arguments[0]}")
    return result.stdout.strip()


def git(worktree, *arguments):
    """在明确的工作目录执行 Git。"""
    return command(["git", "-C", worktree, *arguments])


def copy_path(source, target):
    """复制独立文件，使用写时复制加速；不创建硬链接。"""
    target.parent.mkdir(parents=True, exist_ok=True)
    command(["cp", "-aL", "--reflink=auto", source, target], timeout=300)


def remove_owned(path, owner):
    """删除登记根目录中的子目录，拒绝越界和符号链接。"""
    path, owner = Path(path), Path(owner).resolve()
    if path.is_symlink() or not path.resolve().is_relative_to(owner) or path.resolve() == owner:
        raise DevError(f"清理位置不属于本环境：{path}")
    if path.exists():
        shutil.rmtree(path)


@contextmanager
def locked(path):
    """同一环境的启动、调试和收尾不可交错；异常退出后系统自动释放。"""
    import fcntl
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a") as handle:
        try:
            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise DevError("这个环境已有命令在运行，请等待它结束；其他环境仍可使用") from error
        try:
            yield
        finally:
            fcntl.flock(handle, fcntl.LOCK_UN)


def process_stat(pid):
    """读取 Linux 进程状态，进程已经消失时返回空。"""
    try:
        return (Path("/proc") / str(pid) / "stat").read_bytes().rsplit(b")", 1)[1].split()
    except (FileNotFoundError, ProcessLookupError):
        return None


def process_identity(pid):
    """读取进程开始标记，退出和僵尸进程均返回空。"""
    fields = process_stat(pid)
    return fields[19].decode() if fields and fields[0] not in (b"Z", b"X") else None


def group_running(group):
    """检查组内是否还有能运行的进程，忽略已经退出的进程。"""
    for entry in Path("/proc").glob("[0-9]*"):
        try:
            fields = process_stat(entry.name)
        except PermissionError:
            continue
        if fields and int(fields[2]) == group and fields[0] not in (b"Z", b"X"):
            return True
    return False


def stop_validation_group(process):
    """停止验证命令及同组子进程，全部退出后再结束本次验证。"""
    signals = signal.pthread_sigmask(signal.SIG_BLOCK, {signal.SIGINT, signal.SIGTERM})
    try:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        while group_running(process.pid):
            time.sleep(0.02)
        process.wait()
    finally:
        signal.pthread_sigmask(signal.SIG_SETMASK, signals)


def interrupt_validation(signum, _frame):
    """让系统的终止请求经过验证命令的收尾流程。"""
    raise SystemExit(128 + signum)


def file_digest(paths, root):
    """记录实际输入内容，包含未提交文件，避免只靠时间戳判断版本。"""
    digest = hashlib.sha256()
    files = {}
    for path in sorted(paths):
        if path.is_file():
            relative = path.relative_to(root).as_posix()
            with path.open("rb") as handle:
                value = hashlib.file_digest(handle, "sha256").hexdigest()
            files[relative] = value
            digest.update((relative + "\0" + value + "\n").encode())
    return {"sha256": digest.hexdigest(), "files": files}


def mod_inputs(worktree):
    """登记发布清单中的代码、资源、本地化和依赖。"""
    paths = []
    for entry in MOD_ENTRIES:
        path = worktree / entry
        paths.extend(path.rglob("*") if path.is_dir() else [path])
    return file_digest(paths, worktree)


class Environment:
    """一个 worktree 下具名环境的路径、归属和生命周期。"""

    def __init__(self, worktree, root, name="default"):
        """由真实工作目录生成稳定编号，避免分支改名和同名目录发生混淆。"""
        self.worktree = Path(git(Path(worktree).resolve(), "rev-parse", "--show-toplevel")).resolve()
        if not (self.worktree / "mod.json").is_file() or read_json(self.worktree / "mod.json").get("GUID") != "CULTIWAY":
            raise DevError("请选择 Cultiway 仓库的工作目录")
        self.root = Path(root).expanduser().resolve()
        if not name or any(char not in "abcdefghijklmnopqrstuvwxyz0123456789-" for char in name):
            raise DevError("环境名称只能使用小写字母、数字和短横线")
        if self.root.is_relative_to(self.worktree) or self.worktree.is_relative_to(self.root):
            raise DevError("运行目录与开发工作目录必须分开放置")
        self.name = name
        self.id = hashlib.sha256(str(self.worktree).encode()).hexdigest()[:12] + "-" + name
        self.directory = self.root / "environments" / self.id
        if self.directory.is_symlink():
            raise DevError("环境目录不能是符号链接")
        self.record = self.directory / "environment.json"
        self.game = self.directory / "game"
        self.profile = self.directory / "profile"

    def load(self):
        """读取并核对磁盘上的环境归属。"""
        if not self.record.exists():
            raise DevError("当前 worktree 尚未准备这个环境，请先执行 setup")
        state = read_json(self.record)
        if state.get("worktree") != str(self.worktree) or state.get("id") != self.id:
            raise DevError("环境登记与当前 worktree 不匹配")
        if state["status"] == "uncertain":
            state["validationFailed"] = True
        return state

    def save(self, state):
        """保存环境状态。"""
        write_json(self.record, state)

    def check_process(self, state):
        """核对进程开始身份、可执行文件和环境标记；不认领外部进程。"""
        launch = state.get("launch")
        if not launch or process_identity(launch["pid"]) is None:
            return False
        process = Path("/proc") / str(launch["pid"])
        expected = ("CULTIWAY_DEV_RUN=" + launch["id"]).encode()
        if (process_identity(launch["pid"]) != launch["processStart"] or
                (process / "exe").resolve() != (self.game / "worldbox").resolve() or
                expected not in (process / "environ").read_bytes().split(b"\0")):
            raise DevError("记录的进程身份已改变，拒绝连接或终止它")
        return True

    def setup(self, game_source=None, toolbox_source=None, software_rendering=None):
        """准备私有游戏及客户端，并写入当前 worktree 的开发引用。"""
        config_path = self.root / "config.json"
        config = read_json(config_path) if config_path.exists() else {}
        old = self.load() if self.record.exists() else {}
        if software_rendering is not None:
            config["softwareRendering"] = software_rendering
        elif "softwareRendering" in old:
            config["softwareRendering"] = old["softwareRendering"]
        for key, supplied in (("gameSource", game_source), ("toolboxSource", toolbox_source)):
            if supplied:
                config[key] = str(Path(supplied).expanduser().resolve())
            elif key in old:
                config[key] = old[key]
        if not all(key in config for key in ("gameSource", "toolboxSource")):
            raise DevError("首次 setup 请指定 --game 游戏目录和 --toolbox DebugToolbox目录")
        source, toolbox = Path(config["gameSource"]), Path(config["toolboxSource"])
        required = (source / "worldbox", source / "UnityPlayer.so", source / "libdoorstop.so",
                    source / "BepInEx/core/BepInEx.Preloader.dll",
                    source / "worldbox_Data/Managed/UnityEngine.CoreModule.dll",
                    source / "worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll",
                    toolbox / "Runtime/Transport/ScriptRpcServer.cs",
                    toolbox / "Client/DebugToolbox.Client.csproj")
        for path in required:
            if not path.is_file():
                raise DevError(f"缺少本机依赖：{path}")
        for dependency in (source, toolbox):
            if self.root.is_relative_to(dependency) or dependency.is_relative_to(self.root):
                raise DevError("运行目录必须位于游戏和调试工具来源之外")
        template_paths = [path for path in (source / "worldbox_Data").rglob("*")
                          if "StreamingAssets/mods" not in path.relative_to(source).as_posix()]
        template_paths += list((source / "worldbox_Data/StreamingAssets/mods").glob("NeoModLoader*.dll"))
        template_paths += list((source / "BepInEx/core").rglob("*"))
        template_paths += list((source / "BepInEx/config").rglob("*"))
        template_paths += list((source / "worldbox_Data/StreamingAssets/mods/NML/Assemblies").glob("*.dll"))
        template_paths += [source / "worldbox_Data/StreamingAssets/mods/NML/Assembly-CSharp-Publicized.dll"]
        template_paths += [source / "worldbox", source / "UnityPlayer.so", source / "libdoorstop.so"]
        template = file_digest(template_paths, source)["sha256"]
        if old.get("launch") and self.check_process(old):
            if (template != old["template"] or str(toolbox) != old["toolboxSource"] or
                    config.get("softwareRendering", False) != old.get("softwareRendering", False)):
                raise DevError("游戏运行时不能更换依赖，请先 stop")
            return self.status(old)
        state = dict(old, schema=1, id=self.id, name=self.name, worktree=str(self.worktree),
                     commonDir=git(self.worktree, "rev-parse", "--path-format=absolute", "--git-common-dir"),
                     gameSource=str(source), toolboxSource=str(toolbox), template=template,
                     softwareRendering=config.get("softwareRendering", False),
                     status="preparing")
        self.save(state)
        marker = self.game / ".cultiway-template.json"
        if not marker.exists() or read_json(marker).get("sha256") != template:
            remove_owned(self.game, self.directory)
            self.game.mkdir(parents=True)
            for name in ("worldbox", "UnityPlayer.so", "libdoorstop.so"):
                copy_path(source / name, self.game / name)
            # 原版资料单独复制；加载器目录只采用明确的依赖，不带玩家缓存与启用记录。
            data = self.game / "worldbox_Data"
            data.mkdir()
            for path in (source / "worldbox_Data").iterdir():
                if path.name != "StreamingAssets":
                    copy_path(path, data / path.name)
            streaming = data / "StreamingAssets"
            streaming.mkdir()
            for path in (source / "worldbox_Data/StreamingAssets").iterdir():
                if path.name not in ("mods", "mod_loading_logs.log"):
                    copy_path(path, streaming / path.name)
            native_mods = streaming / "mods"
            native_mods.mkdir()
            for path in (source / "worldbox_Data/StreamingAssets/mods").glob("NeoModLoader.*"):
                copy_path(path, native_mods / path.name)
            nml = native_mods / "NML"
            nml.mkdir()
            for name in ("Assemblies", "Assembly-CSharp-Publicized.dll", "commit"):
                copy_path(source / "worldbox_Data/StreamingAssets/mods/NML" / name, nml / name)
            copy_path(source / "BepInEx/core", self.game / "BepInEx/core")
            copy_path(source / "BepInEx/config", self.game / "BepInEx/config")
            write_json(marker, {"sha256": template})
        state["client"] = str(self.prepare_client(toolbox))
        self.profile.mkdir(parents=True, exist_ok=True)
        options = self.profile / "unity3d/mkarpenko/WorldBox/worldboxData"
        if not options.exists():
            write_json(options, {"tutorialFinished": True, "list": [
                {"name": "experimental", "boolVal": True},
                {"name": "last_used_version", "stringVal": "0.51.2"},
                {"name": "language", "stringVal": "cz"},
                {"name": "autosaves", "boolVal": False},
                {"name": "preload_windows", "boolVal": False},
                {"name": "fullscreen", "boolVal": False}]})
        props = '<Project><PropertyGroup><GameRootPath>' + escape(str(self.game))
        props += '</GameRootPath></PropertyGroup></Project>\n'
        (self.worktree / ".cultiway-dev.props").write_text(props, encoding="utf-8")
        state["status"] = "prepared"
        self.save(state)
        with locked(self.root / "locks/config.lock"):
            write_json(config_path, config)
        return self.status(state)

    def prepare_client(self, toolbox):
        """按实际客户端源码构建一次，不改动相邻工具仓库的输出。"""
        sources = [path for path in (toolbox / "Client").iterdir()
                   if path.suffix in (".client", ".csproj")]
        key = file_digest(sources, toolbox)["sha256"][:20]
        target = self.root / "tools" / key
        client = target / "bin/DebugToolbox.Client.dll"
        with locked(self.root / "locks" / ("client-" + key + ".lock")):
            if not client.exists():
                source = target / "source"
                source.mkdir(parents=True, exist_ok=True)
                for path in sources:
                    shutil.copy2(path, source / path.name)
                command(["dotnet", "build", source / "DebugToolbox.Client.csproj", "-c", "Debug",
                         "-o", target / "bin", "--nologo"], timeout=180)
        return client

    def snapshot(self, state):
        """启动前冻结代码与资源；不改动正在运行的快照。"""
        before = mod_inputs(self.worktree)
        mods = self.game / "Mods"
        toolbox = Path(state["toolboxSource"])
        names = [name for name in git(toolbox, "ls-files", "-z").split("\0")
                 if name and (toolbox / name).is_file() and
                 not name.startswith(("Client/", "bin/", "obj/", ".git/"))]
        names += [path.relative_to(toolbox).as_posix() for path in (toolbox / "Assemblies").glob("*.dll")]
        toolbox_inputs = file_digest([toolbox / name for name in names], toolbox)
        if (state.get("inputs", {}).get("sha256") == before["sha256"] and
                state.get("toolboxInputs") == toolbox_inputs and
                mod_inputs(mods / "Cultiway") == before):
            return before["sha256"]
        old_artifacts = mods / "Cultiway/artifacts"
        artifacts = self.directory / "control/mod-artifacts"
        if old_artifacts.is_dir() and not old_artifacts.is_symlink():
            artifacts.mkdir(parents=True, exist_ok=True)
            shutil.copytree(old_artifacts, artifacts, dirs_exist_ok=True)
        remove_owned(mods, self.directory)
        cultiway = mods / "Cultiway"
        cultiway.mkdir(parents=True)
        for entry in MOD_ENTRIES:
            path = self.worktree / entry
            if path.exists():
                copy_path(path, cultiway / entry)
        if mod_inputs(cultiway) != before or mod_inputs(self.worktree) != before:
            raise DevError("准备快照时开发文件发生变化，请停止写入后重新 start")
        # 工具快照采用受 Git 管理的运行文件，保留本机对这些文件的修改。
        target = mods / "DebugToolbox"
        for name in names:
            copy_path(toolbox / name, target / name)
        if file_digest([toolbox / name for name in names], toolbox) != toolbox_inputs:
            raise DevError("准备快照时调试工具发生变化，请停止写入后重新 start")
        state["toolboxInputs"] = toolbox_inputs
        artifacts.mkdir(parents=True, exist_ok=True)
        (cultiway / "artifacts").symlink_to(artifacts, target_is_directory=True)
        cache = self.game / "worldbox_Data/StreamingAssets/mods/NML"
        remove_owned(cache / "CompiledMods", self.directory)
        for name in ("mod_compile_records.json", "disabled_mods.txt"):
            (cache / name).unlink(missing_ok=True)
        state["inputs"] = dict(before, commit=git(self.worktree, "rev-parse", "HEAD"),
                               branch=git(self.worktree, "branch", "--show-current"))
        return before["sha256"]

    def child_environment(self, state):
        """为客户端和脚本统一提供连接目录、配置和输出位置。"""
        run = self.directory / "runs" / state["launch"]["id"]
        return dict(os.environ, TMPDIR=str(run / "temp"), XDG_CONFIG_HOME=str(self.profile),
                    CULTIWAY_DEV_ROOT=str(self.root), CULTIWAY_DEV_ENV=self.id,
                    CULTIWAY_DEV_RUN=state["launch"]["id"], CULTIWAY_DEV_PID=str(state["launch"]["pid"]),
                    CULTIWAY_DEV_PROFILE=str(self.profile), CULTIWAY_DEV_GAME=str(self.game / "worldbox"),
                    CULTIWAY_DEV_OUTPUT=str(run / "output"), CULTIWAY_DEBUG_CLIENT=state["client"],
                    CULTIWAY_DEMO_CONTROL=str(self.directory / "control/spell_demo"),
                    CULTIWAY_BASELINE_ROOT=str(self.directory / "control/baseline"),
                    CULTIWAY_BASELINE_GAME=str(self.game / "worldbox"))

    def rpc(self, state, *arguments, timeout=40):
        """只调用所属会话的现有客户端，客户端结果失败也视为命令失败。"""
        if not self.check_process(state):
            raise DevError("本环境游戏尚未运行，请先 start")
        session = self.directory / "runs" / state["launch"]["id"] / "temp/DebugToolbox/session.json"
        if read_json(session)["processId"] != state["launch"]["pid"]:
            raise DevError("调试会话不属于本环境，拒绝发送命令")
        result = json.loads(command(["dotnet", state["client"], *arguments], cwd=self.worktree,
                                    env=self.child_environment(state), timeout=timeout))
        if isinstance(result, dict) and result.get("Success") is False:
            raise DevError(json.dumps(result, ensure_ascii=False))
        return result

    def probe(self, state):
        """握手后核对游戏内真实路径和模组，才允许进行后续修改。"""
        code = '''return new { pid = System.Diagnostics.Process.GetCurrentProcess().Id,
            run = Environment.GetEnvironmentVariable("CULTIWAY_DEV_RUN"),
            data = UnityEngine.Application.persistentDataPath,
            game = UnityEngine.Application.dataPath,
            mod = Cultiway.ModClass.I == null ? null : Cultiway.ModClass.I.GetDeclaration().FolderPath,
            ready = !SmoothLoader.isLoading() && Cultiway.ModClass.I != null && Cultiway.ModClass.I.W != null,
            module = typeof(Cultiway.ModClass).Assembly.ManifestModule.ModuleVersionId.ToString() };'''
        result = self.rpc(state, "eval", code).get("Result", {})
        if (result.get("pid") != state["launch"]["pid"] or result.get("run") != state["launch"]["id"] or
                Path(result.get("game", "/")).resolve() != self.game / "worldbox_Data" or
                not Path(result.get("data", "/")).resolve().is_relative_to(self.profile) or
                Path(result.get("mod") or "/").resolve() != self.game / "Mods/Cultiway"):
            raise DevError("游戏内身份或资料目录与本环境不匹配")
        return result

    def require_current(self, state):
        """开发输入改变后要求明确重启，避免用旧程序集验证新代码。"""
        if not self.check_process(state):
            raise DevError("本环境游戏尚未运行，请先 start")
        if mod_inputs(self.worktree)["sha256"] != state["inputs"]["sha256"]:
            raise DevError("开发代码或资源已改变，请先 stop，再 start 准备新快照")
        identity = self.probe(state)
        if not identity["ready"]:
            raise DevError("本环境仍在加载，请稍后重试；不会创建另一个游戏")
        return identity

    def start(self, timeout=240):
        """启动独立游戏；超时保留同一进程和日志，供 status 或 stop 处理。"""
        state = self.load()
        if self.check_process(state):
            if state["status"] == "uncertain":
                raise DevError("上次命令状态不确定，请先 stop 再 start")
            identity = self.require_current(state)
            state.update(status="running", identity=identity)
            self.save(state)
            return self.status(state)
        if not self.game.exists():
            raise DevError("游戏环境已回收，请先 setup")
        self.snapshot(state)
        run_id = stamp() + "-" + uuid.uuid4().hex[:8]
        run = self.directory / "runs" / run_id
        (run / "temp").mkdir(parents=True)
        (run / "output").mkdir()
        state["launch"] = {"id": run_id, "pid": 0}
        env = self.child_environment(state)
        env.update(DOORSTOP_ENABLED="1", DOORSTOP_IGNORE_DISABLED_ENV="0",
                   DOORSTOP_TARGET_ASSEMBLY=str(self.game / "BepInEx/core/BepInEx.Preloader.dll"),
                   LD_PRELOAD=str(self.game / "libdoorstop.so"), SDL_VIDEO_X11_WMCLASS="ai-owned")
        if state.get("softwareRendering"):
            env["LIBGL_ALWAYS_SOFTWARE"] = "1"
            env["__GLX_VENDOR_LIBRARY_NAME"] = "mesa"
        launcher = shutil.which("ai-gui")
        args = ([launcher] if launcher else []) + [str(self.game / "worldbox")]
        args += ["-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
                 "-logFile", str(run / "player.log")]
        with (run / "console.log").open("w") as log:
            process = subprocess.Popen(args, cwd=self.game, env=env, stdout=log,
                                       stderr=subprocess.STDOUT, start_new_session=True)
        state["launch"].update(pid=process.pid, processStart=process_identity(process.pid))
        state["status"] = "starting"
        self.save(state)
        write_json(run / "inputs.json", state["inputs"])
        session = run / "temp/DebugToolbox/session.json"
        deadline, last_error = time.monotonic() + timeout, "尚未生成调试会话"
        while time.monotonic() < deadline:
            if process.poll() is not None:
                state["status"] = "stopped"
                self.save(state)
                raise DevError(f"游戏已经退出，请查看 {run / 'console.log'} 和 player.log")
            if session.exists():
                try:
                    self.rpc(state, "hello")
                    identity = self.probe(state)
                    if identity["ready"]:
                        state.update(status="running", identity=identity)
                        self.save(state)
                        write_json(run / "identity.json", identity)
                        return self.status(state)
                except (DevError, json.JSONDecodeError, FileNotFoundError) as error:
                    last_error = str(error)
            time.sleep(2)
        raise DevError(f"本环境尚未就绪，已保留进程和日志：{run}；{last_error}")

    def execute(self, arguments, script=False, timeout=120):
        """独占执行一条脚本或已有验证命令，保存实际输入和结果。"""
        state = self.load()
        if state["status"] == "uncertain":
            raise DevError("上次命令被中断，游戏内操作可能仍未结束，请先 stop 再 start")
        self.require_current(state)
        case = self.directory / "runs" / state["launch"]["id"] / "cases" / (stamp() + "-" + uuid.uuid4().hex[:8])
        case.mkdir(parents=True)
        state.update(status="uncertain", validationFailed=True)
        self.save(state)
        write_json(case / "request.json", {"arguments": arguments, "script": script,
                                           "input": state["inputs"]["sha256"], "started": stamp()})
        try:
            if script:
                source = Path(arguments[0]).resolve()
                shutil.copy2(source, case / "input.script")
                result = self.rpc(state, "run", str(case / "input.script"), str(timeout * 1000), timeout=timeout + 10)
                write_json(case / "result.json", result)
            else:
                env = dict(self.child_environment(state), CULTIWAY_DEV_ACTIVE="1")
                with subprocess.Popen(arguments, cwd=self.worktree, env=env, start_new_session=True,
                                      stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True) as process:
                    try:
                        stdout, stderr = process.communicate(timeout=timeout)
                    except BaseException:
                        stop_validation_group(process)
                        raise
                (case / "stdout.log").write_text(stdout, encoding="utf-8")
                (case / "stderr.log").write_text(stderr, encoding="utf-8")
                if process.returncode:
                    raise DevError(f"验证命令失败，输出保存在 {case}")
                result = {"exitCode": 0, "stdout": str(case / "stdout.log"), "stderr": str(case / "stderr.log")}
        except BaseException as error:
            write_json(case / "failure.json", {"error": str(error), "uncertain": True})
            raise
        state.update(status="running", validationFailed=False)
        self.save(state)
        return {"evidence": str(case), "result": result}

    def stop(self):
        """退出已核实身份的游戏；卡住时只终止该进程。"""
        state = self.load()
        alive = self.check_process(state)
        if not alive and state["status"] in ("delivered", "cleaned", "merged"):
            return self.status(state)
        if alive:
            pid = state["launch"]["pid"]
            descriptor = os.pidfd_open(pid)
            try:
                # 先固定进程句柄，再核对归属，避免进程号复用造成误终止。
                self.check_process(state)
                try:
                    self.rpc(state, "eval", "UnityEngine.Application.Quit(); return true;", timeout=5)
                except (DevError, subprocess.TimeoutExpired, FileNotFoundError, json.JSONDecodeError):
                    pass
                for attempt in range(60):
                    if process_identity(pid) is None:
                        break
                    if attempt == 20:
                        signal.pidfd_send_signal(descriptor, signal.SIGTERM)
                    if attempt == 50:
                        signal.pidfd_send_signal(descriptor, signal.SIGKILL)
                    time.sleep(.2)
                if process_identity(pid) is not None:
                    raise DevError("所属游戏仍未退出，保留环境")
            finally:
                os.close(descriptor)
        state["status"] = "stopped"
        if state.get("launch"):
            destination = self.directory / "runs" / state["launch"]["id"] / "game-logs"
            paths = list(self.game.glob("*.log")) + [self.game / "Mods/Cultiway/Logs",
                self.game / "BepInEx/LogOutput.log", self.game / "worldbox_Data/StreamingAssets/mod_loading_logs.log"]
            for path in paths:
                if path.exists():
                    target = destination / path.relative_to(self.game)
                    if target.is_dir():
                        remove_owned(target, self.directory)
                    copy_path(path, target)
        self.save(state)
        return self.status(state)

    def status(self, state=None):
        """输出不包含会话令牌的状态和常用位置。"""
        state = state or self.load()
        launch = state.get("launch", {})
        return {"id": self.id, "worktree": str(self.worktree), "status": state["status"],
                "running": self.check_process(state), "pid": launch.get("pid"),
                "game": str(self.game), "profile": str(self.profile),
                "softwareRendering": state.get("softwareRendering", False),
                "session": str(self.directory / "runs" / launch["id"] / "temp/DebugToolbox/session.json") if launch else None,
                "evidence": state.get("evidence"), "pr": state.get("pr")}

    def finish(self, pr):
        """核对 PR 和提交后归档证据，再回收游戏；分支和 worktree 保留。"""
        state = self.load()
        if state.get("validationFailed"):
            raise DevError("上次验证未成功，请先 stop，再 start 并重新完成验证")
        if state["status"] == "delivered":
            if pr != state["pr"]:
                raise DevError("PR 与交付记录不同，保留现有文件")
            if (git(self.worktree, "rev-parse", "HEAD") != state["deliveredHead"] or
                    git(self.worktree, "status", "--porcelain")):
                raise DevError("交付后已有新修改，请重新 setup、验证和交付")
            return self.remove_runtime(state)
        info = pr_info(self.worktree, pr)
        head = git(self.worktree, "rev-parse", "HEAD")
        if (info["state"] not in ("OPEN", "MERGED") or info["headRefOid"] != head or
                info["headRefName"] != git(self.worktree, "branch", "--show-current")):
            raise DevError("PR 未处于可交付状态，或本地提交还未推送到这个 PR")
        if git(self.worktree, "status", "--porcelain"):
            raise DevError("还有未提交文件，请先明确处理这些修改再交付")
        if not state.get("inputs") or mod_inputs(self.worktree)["sha256"] != state["inputs"]["sha256"]:
            raise DevError("实际运行快照与当前输入不同，请先完成对应验证")
        return self.archive("delivered", pr=info["url"], deliveredHead=head, prInfo=info)

    def archive(self, outcome, **metadata):
        """正式交付或结束试用时，先保留完整证据再回收所属运行文件。"""
        previous = self.load()
        if previous["status"] in ("delivered", "cleaned") and not self.check_process(previous):
            return self.remove_runtime(previous)
        self.stop()
        state = self.load()
        archive = self.root / "evidence" / self.id / stamp()
        archive.mkdir(parents=True)
        write_json(archive / "environment.json", state)
        if "prInfo" in metadata:
            write_json(archive / "pr.json", metadata.pop("prInfo"))
        # 先完整归档再删除。运行会话和令牌不进入可分享证据。
        runs = self.directory / "runs"
        if runs.exists():
            shutil.copytree(runs, archive / "runs", ignore=shutil.ignore_patterns("temp"))
        for name in ("profile", "control"):
            if (self.directory / name).exists():
                copy_path(self.directory / name, archive / name)
        if (self.game / "Mods").exists():
            copy_path(self.game / "Mods", archive / "inputs")
        game_logs = list(self.game.glob("*.log"))
        game_logs += [self.game / "BepInEx/LogOutput.log",
                      self.game / "worldbox_Data/StreamingAssets/mod_loading_logs.log"]
        for path in game_logs:
            if path.exists():
                shutil.copy2(path, archive / path.name)
        state.update(metadata, status=outcome, evidence=str(archive))
        self.save(state)
        return self.remove_runtime(state)

    def remove_runtime(self, state):
        """复用已经保存的归档，清理可能仍有残留的运行目录。"""
        evidence = state.get("evidence")
        if not evidence or not Path(evidence).is_dir():
            raise DevError("找不到归档证据，保留剩余运行目录")
        for name in ("game", "profile", "control", "runs"):
            remove_owned(self.directory / name, self.directory)
        return self.status(state)

    def reap(self, remove_worktree=False):
        """核对远端实际合并状态，输出或执行普通 Git worktree 回收。"""
        state = self.load()
        if state["status"] != "delivered":
            raise DevError("请先 finish 交付并归档环境，再回收 worktree")
        info = pr_info(self.worktree, state["pr"])
        if info["state"] != "MERGED" or info["headRefOid"] != state["deliveredHead"]:
            raise DevError("PR 尚未合并，或合并的内容与交付记录不同，保留 worktree")
        if git(self.worktree, "status", "--porcelain") or git(self.worktree, "rev-parse", "HEAD") != state["deliveredHead"]:
            raise DevError("worktree 中有交付后的修改，保留目录")
        common = Path(state["commonDir"])
        if common == self.worktree / ".git":
            raise DevError("不能回收主工作目录")
        if not remove_worktree:
            return {"eligible": True, "worktree": str(self.worktree), "evidence": state["evidence"],
                    "next": "应用管理的 worktree 请使用应用归档；普通 Git worktree 可加 --remove-worktree"}
        if self.worktree.is_relative_to(Path(os.environ.get("CODEX_HOME", Path.home() / ".codex")) / "worktrees"):
            raise DevError("这是应用管理的 worktree，请使用应用归档工具")
        command(["git", "--git-dir", common, "worktree", "remove", self.worktree], cwd=self.root)
        state["status"] = "merged"
        self.save(state)
        return {"removed": str(self.worktree), "evidence": state["evidence"]}


def pr_info(worktree, pr):
    """只读查询当前仓库中的 PR，拒绝其他仓库的同名分支。"""
    repository = command(["gh", "repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner"], cwd=worktree)
    if not pr.startswith("https://github.com/" + repository + "/pull/"):
        raise DevError("PR 地址不属于当前仓库")
    return json.loads(command(["gh", "pr", "view", pr, "--repo", repository, "--json",
                               "url,state,headRefOid,headRefName,isDraft,mergedAt"], cwd=worktree))


def main():
    """所有修改命令共用环境锁，输出 JSON 供人和 skill 使用。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--worktree", type=Path, default=Path.cwd())
    parser.add_argument("--root", type=Path, default=Path(os.environ.get("CULTIWAY_DEV_ROOT", DEFAULT_ROOT)))
    parser.add_argument("--name", default="default")
    sub = parser.add_subparsers(dest="action", required=True)
    setup = sub.add_parser("setup", help="一次准备独立环境")
    setup.add_argument("--game", type=Path)
    setup.add_argument("--toolbox", type=Path)
    setup.add_argument("--software-rendering", action=argparse.BooleanOptionalAction, default=None,
                       help="为本机驱动不可用的测试环境显式启用软件渲染；不用于性能测量")
    sub.add_parser("doctor", help="检查已准备的依赖和环境")
    sub.add_parser("status", help="查看所属环境和连接位置")
    start = sub.add_parser("start", help="同步快照并启动游戏")
    start.add_argument("--timeout", type=int, default=240)
    sub.add_parser("stop", help="仅退出所属游戏，保留测试资料")
    sub.add_parser("clean", help="结束试用或放弃任务时，先归档再回收游戏，保留代码")
    run = sub.add_parser("run", help="执行一个游戏内脚本")
    run.add_argument("script", type=Path)
    run.add_argument("--timeout", type=int, default=120)
    execute = sub.add_parser("exec", help="让已有验证命令继承独立环境")
    execute.add_argument("--timeout", type=int, default=600)
    execute.add_argument("command", nargs=argparse.REMAINDER)
    finish = sub.add_parser("finish", help="交付 PR 后归档并回收游戏环境")
    finish.add_argument("--pr", required=True)
    reap = sub.add_parser("reap", help="确认 PR 合并后检查 worktree 回收条件")
    reap.add_argument("--remove-worktree", action="store_true")
    args = parser.parse_args()
    if sys.platform != "linux":
        raise DevError("第一版仅支持 Linux 原生游戏；Windows 用户资料隔离尚未实现")
    if sys.version_info < (3, 11):
        raise DevError("请使用 Python 3.11 或更新版本")
    os.umask(0o077)
    if args.action in ("run", "exec"):
        signal.signal(signal.SIGTERM, interrupt_validation)
    environment = Environment(args.worktree, args.root, args.name)
    if args.action == "status":
        print(json.dumps(environment.status(), ensure_ascii=False, indent=2))
        return
    with locked(environment.root / "locks" / (environment.id + ".lock")):
        if args.action == "setup":
            result = environment.setup(args.game, args.toolbox, args.software_rendering)
        elif args.action in ("status", "doctor"):
            result = environment.status()
            if args.action == "doctor":
                state = environment.load()
                for path in (environment.game / "worldbox", Path(state["client"]),
                             environment.worktree / ".cultiway-dev.props"):
                    if not path.exists():
                        raise DevError(f"环境需要重新 setup，缺少：{path}")
        elif args.action == "start":
            result = environment.start(args.timeout)
        elif args.action == "stop":
            result = environment.stop()
        elif args.action == "clean":
            result = environment.archive("cleaned")
        elif args.action == "run":
            result = environment.execute([str(args.script)], script=True, timeout=args.timeout)
        elif args.action == "exec":
            arguments = args.command[1:] if args.command[:1] == ["--"] else args.command
            if not arguments:
                raise DevError("exec 后请指定验证命令")
            result = environment.execute(arguments, timeout=args.timeout)
        elif args.action == "finish":
            result = environment.finish(args.pr)
        else:
            result = environment.reap(args.remove_worktree)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (DevError, OSError, ValueError, subprocess.TimeoutExpired) as error:
        print(json.dumps({"error": str(error)}, ensure_ascii=False), file=sys.stderr)
        sys.exit(1)
