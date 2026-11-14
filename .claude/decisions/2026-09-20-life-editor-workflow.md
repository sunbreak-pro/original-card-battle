# life-editor の開発環境を移し、life-editor を Issue の代わりにしない

決めた日は 2026-09-20 です。life-editor を Issue の代わりにしない点は、こうだいさんの決定です。
元の文があった場所は、`.claude/CLAUDE.md` の「Development Workflows」の冒頭と、「判断の控えは life-editor」の節です。

## 何を決めたか

- この日に、life-editor の開発環境を、このリポジトリへ移しました。手順はスキルに任せ、入口の文書の「Development Workflows」の節は、このリポジトリの決まりだけを持ちます。
- life-editor は、Issue の代わりにしません。life-editor に置くのは、判断の控えと調査結果（Note）と、次にやること（Todo）だけです。課題の状態・担当・PR とのつながりは、GitHub の Issue に置きます。

## なぜか

- life-editor は、ステータスが 2 値しかなく、コメントがありません。同期は後勝ちで、記録が消えることがあります。そのため、課題の記録は GitHub の Issue に残します。
- 入口の文書は、2 つの役目を次のたとえで分けています。GitHub は在庫棚（課題の正確な一覧）で、life-editor は献立表（何を作るか決めて記録する場所）です。
- 開発環境を移した理由は、入口の文書に書かれていませんでした。

## やめた案

入口の文書に、やめた案の記録はありませんでした。claude-dotfiles にも、同じ日の決定の記録 `claude/docs/decisions/2026-09-20-life-editor-not-issue-replacement.md` があります。
