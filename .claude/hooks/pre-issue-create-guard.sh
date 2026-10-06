#!/usr/bin/env bash
# pre-issue-create-guard — `gh issue create` を chat-main 以外で止める
# （CLAUDE.md「Development Workflows」§Issue を立てるのは main だけ / #367）。
#
# プロジェクト固有の規約なので外部 hooks-lib には無く、ここが実体（ラッパではない）。
# 規約は life-editor の「起票は chat-main 一元」（2026-07-11）の移植。life-editor は
# 文章だけで守っていて、レーンが打てば通ってしまう。本リポでは 2026-10 にレーン発の
# Issue が半数を占めたので、機械で止める。
#
# main の判定:
#   - .claude/comm/.session-name が `main` なら通す（一時 worktree の chat-main も含む）
#   - .session-name が無いときは、メインの作業ツリー（git-dir == git-common-dir）なら通す
#   - それ以外（レーンの worktree）は exit 2 で止め、申し送りの書き方を返す
#
# 止めないもの: `gh issue comment` / `view` / `list`、PR の `Closes #n` による close。
# 見ていないもの: `gh api` で issues へ直接 POST する経路（規約で禁止、機械では未検出）。
set -uo pipefail

INPUT="$(cat 2>/dev/null || true)"
if command -v node >/dev/null 2>&1; then
  COMMAND="$(printf '%s' "$INPUT" | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{try{process.stdout.write(String(JSON.parse(s).tool_input?.command??""))}catch{process.stdout.write("")}})' 2>/dev/null || true)"
else
  COMMAND="$INPUT"
fi

# コマンドの先頭か区切り（; && || | ( 改行）の直後に置かれた gh だけを見る。
# 文字列の中の "gh issue create"（コミットメッセージ等）で誤爆しにくくするため。
printf '%s' "$COMMAND" | grep -qE '(^|[;&|(])[[:space:]]*gh[[:space:]]([^;&|]*[[:space:]])?issue[[:space:]]+create([[:space:]]|$)' || exit 0

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="$(tr -d '[:space:]' < "$REPO_ROOT/.claude/comm/.session-name" 2>/dev/null || true)"
[ "$NAME" = "main" ] && exit 0

if [ -z "$NAME" ]; then
  # Both go through `cd && pwd` so Windows C:/ and /c/ spellings compare equal.
  GIT_DIR="$(cd "$REPO_ROOT" && cd "$(git rev-parse --git-dir 2>/dev/null)" 2>/dev/null && pwd || true)"
  COMMON_DIR="$(cd "$REPO_ROOT" && cd "$(git rev-parse --git-common-dir 2>/dev/null)" 2>/dev/null && pwd || true)"
  [ -n "$GIT_DIR" ] && [ "$GIT_DIR" = "$COMMON_DIR" ] && exit 0
fi

{
  echo "[pre-issue-create-guard] BLOCKED: Issue を立てるのは chat-main だけです（.session-name = '${NAME:-未宣言}'）。"
  echo "  代わりに申し送りを 1 件 1 ファイルで書いてください。main が回収して Issue にします。"
  echo "    cp .claude/comm/handoff/_TEMPLATE.md .claude/comm/handoff/\$(date +%Y-%m-%d)-${NAME:-<self>}-<slug>.md"
  echo "  書き方は .claude/comm/README.md の「申し送り」です。既存 Issue へのコメントは止めていません。"
} >&2
exit 2
