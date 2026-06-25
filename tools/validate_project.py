from pathlib import Path
import re, sys
root = Path(__file__).resolve().parents[1]
errors = []
for p in root.rglob('*'):
    if p.is_file() and p.suffix.lower() in {'.axaml','.xaml','.cs','.json','.iss','.ps1'}:
        text = p.read_text(encoding='utf-8', errors='ignore')
        if p.suffix.lower() in {'.axaml','.xaml'}:
            if '#FFFFFF' in text or 'White' in text:
                errors.append(f'Potential bright/white UI usage: {p}')
        if re.search(r'ProcessStartInfo\s*\([^)]*\+|UseShellExecute\s*=\s*true', text):
            errors.append(f'Potential unsafe process launch pattern: {p}')
        if 'ForgeStudio.SDK' in text or '<app.json>' in text:
            errors.append(f'Hub/module artifact detected: {p}')
required = [
    'src/ForgeStudio.Circuit.App/ForgeStudio.Circuit.App.csproj',
    'src/ForgeStudio.Circuit.Core/Security/SecurityValidators.cs',
    'installer/ForgeStudioCircuit.iss',
    'build.ps1',
    'package.ps1'
]
for rel in required:
    if not (root / rel).exists():
        errors.append(f'Missing required file: {rel}')
if errors:
    print('\n'.join(errors))
    sys.exit(1)
print('ForgeStudio Circuit validation passed.')
