import re, sys

path = r"C:\Users\spice\AppData\Local\hermes\profiles\lead-mimo\cache\delegation\live\deleg_f05fe857\task-0.log"

with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find lines with the review content
for i, line in enumerate(lines):
    if 'assistant' in line and 'Files Reviewed' in line:
        # This line has the review
        match = re.search(r'assistant\| (.+)', line)
        if match:
            content = match.group(1)
            content = content.replace('\\n', '\n').replace('\\t', '\t')
            print(content[:10000])
        break
