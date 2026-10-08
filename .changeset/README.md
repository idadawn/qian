# Changesets

`@yue/*` 用 [Changesets](https://github.com/changesets/changesets) 管版本与变更记录：改了包的公开行为就在 PR 里跑 `pnpm changeset`，选受影响的包与升级级别（0.x 期间破坏性变更记 `minor`，见 `packages/yue/README.md`「版本策略」）。`@yue/*` 全部包固定同一版本号（`fixed`），`website` 与 `yue-playground` 不发包。
