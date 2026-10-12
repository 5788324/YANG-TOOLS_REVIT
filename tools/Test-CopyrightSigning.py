from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
checks=[]
def check(name, cond):
    checks.append((name, bool(cond)))

csproj=(ROOT/'src/YangTools.Revit/YangTools.Revit.csproj').read_text(encoding='utf-8')
xaml=(ROOT/'src/YangTools.Revit/UI/SystemSettingsWindow.xaml').read_text(encoding='utf-8')
cs=(ROOT/'src/YangTools.Revit/UI/SystemSettingsWindow.xaml.cs').read_text(encoding='utf-8')
license_text=(ROOT/'LICENSE.md').read_text(encoding='utf-8')
notice=(ROOT/'NOTICE.md').read_text(encoding='utf-8')
third=(ROOT/'THIRD_PARTY_NOTICES.md').read_text(encoding='utf-8')
sign=(ROOT/'tools/Sign-YangToolsRelease.ps1').read_text(encoding='utf-8')
verify=(ROOT/'tools/Verify-YangToolsCodeSignature.ps1').read_text(encoding='utf-8')
newcert=(ROOT/'tools/New-LocalCodeSigningCertificate.ps1').read_text(encoding='utf-8')

for token in [
    '<Authors>Yang</Authors>', '<Company>Yang</Company>', '<Product>YangTools</Product>',
    '<AssemblyVersion>4.0.0.0</AssemblyVersion>', '<FileVersion>4.0.0.0</FileVersion>',
    '<InformationalVersion>4.0.0</InformationalVersion>', '<Copyright>Copyright © 2026 Yang</Copyright>'
]: check('csproj '+token, token in csproj)
for f in ['LICENSE.md','NOTICE.md','THIRD_PARTY_NOTICES.md']:
    check('output '+f, f in csproj and '<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>' in csproj)
for token in ['Text="YangTools"','Text="原作者"','Text="Yang"','Copyright © 2026 Yang','非商业署名许可','允许：免费使用','要求：保留 YangTools','禁止：未经原作者授权','x:Name="SignatureText"']:
    check('about '+token, token in xaml)
for token in ['AssemblyInformationalVersionAttribute','GetAuthenticodeSignerDisplay','X509Certificate.CreateFromSignedFile','未检测到 Authenticode 发布签名']:
    check('about code '+token, token in cs)
for token in ['原作者 **Yang**','允许任何人免费','公开分发','二次开发','禁止的商业使用']:
    check('license '+token, token in license_text)
for token in ['Original Project','Original Author','Based on YangTools']:
    check('notice '+token, token in notice)
for token in ['Newtonsoft.Json','IronPython','MiniExcel','Autodesk Revit API']:
    check('third '+token, token in third)
for token in ['signtool.exe',"'/fd', 'SHA256'",'/tr','signtool verify']:
    check('sign '+token, token in sign)
for token in ['Get-AuthenticodeSignature',"$signature.Status -eq 'Valid'",'AllowUntrustedSelfSigned']:
    check('verify '+token, token in verify)
for token in ['New-SelfSignedCertificate','CodeSigningCert','KeyExportPolicy NonExportable','CN=Yang']:
    check('newcert '+token, token in newcert)

# Never ship private signing materials in source.
forbidden=[]
for p in ROOT.rglob('*'):
    if p.is_file() and p.suffix.lower() in {'.pfx','.p12','.key','.pem'}:
        forbidden.append(str(p.relative_to(ROOT)))
check('no private signing material', not forbidden)

failed=[n for n,v in checks if not v]
print(f'Copyright/signing checks: {len(checks)-len(failed)}/{len(checks)} PASS')
if failed:
    for n in failed: print('FAIL:',n)
    raise SystemExit(1)
