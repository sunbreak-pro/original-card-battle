# MEMORY.md — 移転済み（後方互換のポインタ）

> **正本は `.claude/memory/chat-<自分のチャット名>.md` に移りました**（2026-09-20、life-editor の開発環境を移植）。
> このファイルはもう更新されません。リンク切れを避けるためだけに残しています。

- **自分のチャットの進捗**: `.claude/memory/chat-main.md`（チャット名は `.claude/comm/.session-name` が持つ）
- **全チャットの横断ビュー**: `.claude/memory/INDEX.md`（git 非追跡の生成物。`.claude/hooks/regen-index.sh` が作る）
- **変更履歴**: `.claude/history/chat-main.md`
- **更新のしかた**: `task-tracker` スキル経由（手動編集しない）

移転の理由は、並行チャットが 1 つのファイルの同じ行を書き換えて必ず衝突するためです。詳細は `.claude/CLAUDE.md` の「Development Workflows」節。
