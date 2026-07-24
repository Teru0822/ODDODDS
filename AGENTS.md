## ⚠️ AIエージェントへの必須指示

**作業を開始する前に、このファイル（AGENTS.md）を必ず読むこと。**
その後、[`Docs/01_Rules_and_Policies/04_AI_Agents/AIAgentRules.md`](Docs/01_Rules_and_Policies/04_AI_Agents/AIAgentRules.md) および [`Docs/01_Rules_and_Policies/03_Workflows/GitWorkflow.md`](Docs/01_Rules_and_Policies/03_Workflows/GitWorkflow.md) を読んでから作業に着手すること。

**「簡単な作業だから確認不要」という判断は認めない。**

**ユーザーからのプロンプトが日本語である場合は、説明や返答も必ず日本語で行うこと。**

---

## 1. このファイルの目的
このファイル（`AGENTS.md`）は、本プロジェクト（FEVER CAPITAL）における AI エージェントおよび開発メンバー向けの共通運用ルールの「ハブ（ポータル）」として機能します。

Unity (Unity 6.3 LTS) を用いた複数人開発を円滑に進めるための具体的なルールは、すべて `Docs/` ディレクトリ配下に多階層で整理されています。本ファイルには全体像とルールの優先順位のみを記載し、詳細は各ドキュメントを参照してください。

---

## 2. 参照ルール（ルールの優先順位）
本プロジェクトでは、運用ルールの優先順位を以下の通りとします。

1. 明示された依頼内容
2. `AGENTS.md`（本ファイルへの記載事項）
3. `Docs/01_Rules_and_Policies/` 以下のルール群
4. README / 各種仕様・設計資料 (`Docs/02_Specifications/`) / Issue / タスク管理ツール (`Docs/03_Tasks/`)
5. 既存コードの実装方針

---

## 3. ドキュメント構成・管理ルールのインデックス
プロジェクトのドキュメントやルールは以下のように階層化して管理されています。確認・調査の際はここから辿ってください。

- **ルートディレクトリ**
  - `README.md`: プロジェクトの顔となる重要な情報。
  - `AGENTS.md` (本ファイル): 当インデックスとルールの優先順位。

- **`Docs/01_Rules_and_Policies/` (全体方針・作業ルール群)**
  - 全体に適用される方針、規約、Gitや開発環境の情報を多階層で管理しています。
  - **👉 詳細なルール一覧は [`Overview.md`](Docs/01_Rules_and_Policies/Overview.md) を確認してください。**
    - `01_Team_Policies`: チームの基本方針、禁止事項など
    - `02_Development_Guidelines`: 開発環境共通ルール、Unityルール、コーディング規約など
    - `03_Workflows`: Git・PR運用ルール、タスク対応ルール、ドキュメント管理ルールなど
    - `04_AI_Agents`: AIエージェントに特化した注意点と報告ルールなど

- **`Docs/02_Specifications/` (仕様・設計)**
  - 各機能やモジュール単位の仕様、設計資料、挙動の詳細。

- **`Docs/03_Tasks/` (作業・タスク)**
  - 進行中のタスク、Issueのまとめ、短中期的なマイルストーンなど流動的なもの。
  - 進行管理用: [`TASK.md`](Docs/03_Tasks/TASK.md)

- **`Docs/04_Logs_and_Reports/` (ログ・議事録・事後報告)**
  - 作業ログ、エラー調査ログ、会議議事録、Issue等の事後報告（Report）を格納します。

各ルールの追加やドキュメントの更新方針については、[03_Workflows/DocumentRules.md](Docs/01_Rules_and_Policies/03_Workflows/DocumentRules.md) に従ってください。