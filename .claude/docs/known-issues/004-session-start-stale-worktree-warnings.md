# 004: SessionStart の警告が、放置された worktree と古い .session-name を毎回指す

- Status: Mitigated
- Category: Tooling / Harness
- 発見: 2026-10-12 から `outbox/main/session-start-warnings.md` に警告が積み上がる

## 症状

SessionStart の hook が、次の 2 種類の警告を毎回出します。

- `E: worktree <name> に N 時間以上放置の dirty ファイルあり`
- `D: .session-name (main) は最終 commit より 3 日以上古い`

警告は `.claude/comm/outbox/main/session-start-warnings.md` に追記され続け、2026-10-31 時点で 29KB あります。

## Root Cause

- Workflow（`wf_*`）の実行で作られた worktree が、終わった後も `.claude/worktrees/` に残っていました。dirty の中身は `.claude/settings.local.json` の数行で、本体の作業ではありませんでした。
- 警告の一覧には、既に消えた worktree（`wf_e67aa560-68e-*`）も残っていました。hook が古い状態を読み直していません。
- `.session-name` の警告は、最終 commit の日付だけを見て判定しています。main は作業の少ない期間があるため、正しい名前でも警告が出ます。

## 再発防止

- 放置 worktree は、マージ済みかを件名で確かめてから消します。main に同じ件名の PR merge があれば消して構いません（2026-10-31 に 4 件で実施）。未マージの作業（untracked の `.cs` など）があるものは残します。
- Workflow の後片付けは、終わった時点で worktree を消します（`finished-work` スキルを使います）。
- `outbox/main/` の警告は、読んで処理したら消すか、ファイルの先頭に処理日を書きます。

## 未対応

- hook の判定を直すこと（worktree を一覧から外すか、`.session-name` の判定を commit 日付から外すこと）は、ハーネス側の作業です。未着手です。
