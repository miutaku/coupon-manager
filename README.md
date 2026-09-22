# coupon-manager
クーポンや割引券を一か所で管理するアプリ

# 仕様

## デザイン

Material 3 Expressive を採用。固定ヘッダーは置かず、コンパクトな可変高カードとボトムナビゲーションで構成する。ライト・ダークテーマと日本語・英語表示に対応する。

## インフラ

- frontend .NET 10
  - Blazor Web App
- backend .NET 10
- DB PostgreSQL 18.6
  - マイグレーションの自動運用はデプロイ時に行える
- infra docker compose (k8s ready)
  - k8s manifestを用意


## クーポン表示方法
クーポン表示は、以下の表示方法がある。
- QRコード
- バーコード
  - Code 128
  - JAN / EAN-13
  - EAN-8
- シリアル文字列

がある。

## 通知

- クーポンの有効期限を、指定した日数または時間前に通知できる。
- 通知ルール（何を・いつ通知するか）と通知チャンネル（どこへ通知するか）を分離する。
- Discord Webhook とシェルスクリプト実行に対応する。
- 通知済みイベントを記録し、同じ期限・設定・通知先への重複送信を防ぐ。
- LINE、Slackなどは通知チャンネル実装を追加して拡張できる。

- `COUPON_NOTIFICATION_TYPE`
- `COUPON_ID`
- `COUPON_NAME`
- `COUPON_EXPIRES_ON`
- `COUPON_NOTIFICATION_TITLE`
- `COUPON_NOTIFICATION_BODY`

有効期限は期限日の終端を基準に判定する。

docker-compose.ymlでは既定のタイムゾーンが`Asia/Tokyo`で、`NOTIFICATION_TIME_ZONE`にIANAタイムゾーンIDを指定して変更できる。

# CI/CD

- Github actionsを採用する。
- Docker ImageをReleaseに成果物を出す。

# test

- ドメイン・アプリケーション層: xUnit の単体テスト
- API: integration test
- PostgreSQL を使うテスト: Testcontainers
- UI: Playwright による主要導線テスト
- CI で format、build、test、Docker build を実行

# 実装

README の仕様を、Clean Architecture を意識した次のプロジェクト構成で実装している。

- `CouponManager.Domain`: クーポンとラベルのドメインモデル
- `CouponManager.Application`: DTO、ユースケース、リポジトリ境界
- `CouponManager.Infrastructure`: EF Core / PostgreSQL とマイグレーション
- `CouponManager.Api`: REST API、QR/バーコード生成、画像保存
- `CouponManager.Web`: Material 3 Expressive を意識した Blazor Web App
- `tests`: xUnit、Testcontainers、Playwright のテスト

## 起動方法

### Dockerの場合

```bash
sudo docker compose up -d --build
sudo docker compose ps
```

起動後、Web UI は <http://localhost:8080>、OpenAPI JSON は
<http://localhost:5080/openapi/v1.json> で確認できる。DB マイグレーションは API 起動時に自動適用される。

### kubernatesを使う場合

sampleのmanifestを参考。
Kubernetes 用のひな形は `deploy/k8s/coupon-manager.yaml` にある。これはOSS利用者向けのサンプルであり、適用前に Secret、イメージ名、公開 URL、Ingress、永続ストレージを環境に合わせて変更すること。

## ローカルでテストする場合

.NET SDKが必要。

```bash
dotnet restore CouponManager.slnx
dotnet build CouponManager.slnx
dotnet test CouponManager.slnx
```

UI テストは Playwright の Chromium を一度インストールし、アプリ起動後に `E2E_BASE_URL` を指定して実行する。

```bash
pwsh tests/CouponManager.Ui.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
E2E_BASE_URL=http://localhost:8080 dotnet test tests/CouponManager.Ui.Tests
```

## AI画像解析

クーポン画像の解析には、画像入力とJSON Schema形式の構造化出力に対応したOpenAI互換APIを使用する。WebLLMやサーバー内OCRは使用しない。APIサーバーが画像をVLMへ送り、クーポン名、詳細、有効期限、利用回数、QR／バーコード／シリアルの情報を取得する。レシート内に複数のクーポンがある場合は候補として分けて表示する。

Docker Composeでは次の環境変数を指定する。`AI_BASE_URL`には `/v1` まで含める。

```bash
AI_BASE_URL=http://your-vllm-host:8000/v1 \
AI_MODEL=Qwen/Qwen2.5-VL-3B-Instruct \
AI_API_KEY=optional-key \
sudo -E docker compose up -d --build
```

- `AI_BASE_URL`: OpenAI互換APIのベースURL。未設定の場合、画像解析は無効になる。
- `AI_MODEL`: APIへ送るモデル名。既定値は `Qwen/Qwen2.5-VL-3B-Instruct`。
- `AI_API_KEY`: Bearer認証用。認証のないセルフホストAPIでは空でよい。
- `AI_TIMEOUT_SECONDS`: 解析タイムアウト。既定値180秒、10〜900秒。

画像は設定されたAIサービスへ送信される。通信経路、認証、入力画像やログの保持方針は、利用者が構築するAI基盤側で管理する。

## PWA

Web UIはPWAとしてインストールできる。Android／デスクトップChromeではブラウザのインストール操作、iOS／iPadOSでは共有メニューの「ホーム画面に追加」を使用する。Service Workerを利用するため、`localhost`以外ではHTTPSで公開する必要がある。

クーポンの参照・編集・同期にはWebサーバーとAPIへの接続が必要であり、完全なオフライン動作は行わない。通信できない場合はオフライン画面を表示し、接続復帰後に再読み込みする。

## 現在の既定値

- 使用済みと期限切れは一覧から非表示になり、「使用済み・期限切れも表示」で再表示できる。
- 「使用済み」は残り回数を一度に 0 にする。編集画面では残り回数も修正できる。
- 「1回使用」は残り回数を1減らす。
- 使用済みと期限切れの既定保持期間は30日で、設定画面から1〜3650日の範囲で変更できる。
- 添付画像は API コンテナの永続ボリュームに保存する（最大 10 MB）。
- 登録画面では、設定されたOpenAI互換VLM APIが画像全体を解析し、複数のクーポン候補から選んで入力できる。ブラウザ内モデルとOCRフォールバックは持たない。
- 検索対象は「すべて・名前・詳細・ラベル・コード」。
- 並び順は「登録日時（新/古）・有効期限・名前」。
