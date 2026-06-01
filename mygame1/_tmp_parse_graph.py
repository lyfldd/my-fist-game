import re

with open('Assets/_Game/Config/ItemGraph.asset', 'r', encoding='utf-8') as f:
    text = f.read()

# Count itemName occurrences
total = text.count('itemName:')
print(f'总节点数: {total}')

# Find all item names for not-ready items
# Split into node blocks
blocks = text.split('\n  - itemName: ')
not_ready = []
for block in blocks:
    if 'allSystemsReady: 0' in block:
        name = block.split('\n')[0].strip().strip('"')
        not_ready.append(name)

print(f'缺脚本物品 (allSystemsReady: 0): {len(not_ready)}')
print()
for it in not_ready:
    print(f'  ⚠ {it}')

# Also extract stats from bottom
m_raw = re.search(r'rawMaterialCount: (\d+)', text)
m_dead = re.search(r'deadEndCount: (\d+)', text)
m_core = re.search(r'coreMaterialCount: (\d+)', text)
print(f'\nrawMaterialCount: {m_raw.group(1) if m_raw else "?"}')
print(f'deadEndCount: {m_dead.group(1) if m_dead else "?"}')
print(f'coreMaterialCount: {m_core.group(1) if m_core else "?"}')
