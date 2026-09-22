# Kubernetes sample

`coupon-manager.yaml` はOSS利用者向けの最小構成サンプルです。本番環境へそのまま適用するものではありません。

利用者の環境に合わせて、少なくとも次を変更してください。

- `ghcr.io/OWNER/...` のコンテナイメージ名
- `replace-me` のデータベースパスワード（本番では外部Secret管理を推奨）
- `Ai__BaseUrl`、`Ai__Model`、必要に応じて `AI_API_KEY`（画像入力対応のOpenAI互換API）
- 公開URLとCORS
- Ingress / Gateway API、TLS、DNS
- StorageClass、容量、アクセスモード
- 認証・認可、NetworkPolicy、バックアップ、監視

アプリケーションはこれらの運用方針を強制しません。
