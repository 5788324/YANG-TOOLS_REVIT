# YangTools 3.1.38｜代码签名与版权声明

## 1. 目标

YangTools 不做反编译阻止或 DRM。公开分享策略固定为：

- 允许免费使用；
- 允许公开分发；
- 允许修改与二次开发；
- 禁止未经授权的商业使用、销售、付费分发或收费集成；
- 所有原版/衍生版必须注明原始项目 **YangTools** 与原作者 **Yang**；
- 第三方组件继续遵守各自许可证。

## 2. 程序内版权信息

程序集固定元数据：

- Product: `YangTools`
- Authors / Company: `Yang`
- Version / AssemblyVersion / FileVersion: `3.1.38 / 3.1.38.0 / 3.1.38.0`
- Copyright: `Copyright © 2026 Yang`

“系统设置 → 关于”页显示：

- YangTools / Revit Productivity & Automation Toolkit；
- 当前程序集版本；
- 原作者 Yang；
- Copyright © 2026 Yang；
- 非商业署名许可摘要；
- Revit 2021–2027 独立编译说明；
- 当前 DLL 是否检测到 Authenticode 签名证书。

## 3. 许可文件

源码根目录：

- `LICENSE.md`
- `NOTICE.md`
- `THIRD_PARTY_NOTICES.md`

构建时同时复制到输出目录的 `Docs/`。

## 4. Authenticode 签名流水线

### 4.1 为什么源码包不包含私钥

任何 `.pfx`、私钥、证书密码、签名服务令牌都禁止进入源码 ZIP、Git 或 Google Drive 项目公开交付区。

签名只作用于**正式发布二进制**，不作用于源码 ZIP。

### 4.2 本地/内部自签名

Windows 上可以运行：

```powershell
.\tools\New-LocalCodeSigningCertificate.ps1
```

得到 CurrentUser\My 中不可导出的本地 CodeSigning 证书，再用 Thumbprint：

```powershell
.\tools\Sign-YangToolsRelease.ps1 `
  -Files .\src\YangTools.Revit\bin\x64\2024\YangTools.Revit.dll `
  -CertificateThumbprint <THUMBPRINT> `
  -AllowUntrustedSelfSigned
```

自签名证书只适合开发/内部签名。其他电脑默认不会信任它，不能宣称“受信任发布者”。

### 4.3 正式公开发布签名

获得可信 CA 代码签名证书后，可使用 Windows 证书存储 Thumbprint，或私有 PFX。PFX 密码只通过环境变量提供：

```powershell
$env:YANGTOOLS_SIGN_PFX_PASSWORD = '<只存在当前终端>'
.\tools\Sign-YangToolsRelease.ps1 `
  -Files <YangTools.Revit.dll> `
  -CertificatePath <private-path\YangTools.pfx>
```

默认：

- 文件摘要：SHA-256；
- RFC3161 timestamp；
- timestamp digest：SHA-256；
- 签名后立即 `signtool verify /pa /v`。

### 4.4 独立验证

```powershell
.\tools\Verify-YangToolsCodeSignature.ps1 -Files <YangTools.Revit.dll>
```

正式公开 Release 的硬门禁：`Get-AuthenticodeSignature.Status == Valid`。

## 5. 发布包边界

允许公开：

- 编译后的 YangTools DLL；
- `.addin`；
- 必要运行时依赖；
- LICENSE / NOTICE / THIRD_PARTY_NOTICES；
- 安装脚本/安装包。

不得公开：

- 私钥、PFX、证书密码；
- 开发者本地证书私钥；
- 任何未计划公开的内部凭据。

源码是否公开由作者自行决定；本许可允许获得源码的人在非商业条件下修改和二次开发。
