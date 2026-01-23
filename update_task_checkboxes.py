#!/usr/bin/env python3
"""
Update markdown files to check off completed tasks.
"""
import json
import re
from pathlib import Path

def update_file_checkboxes(filepath: str, tasks_to_check: list):
    """Update checkboxes in a file"""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        modified = False
        updated_count = 0
        for task_info in tasks_to_check:
            # Only update actual checkboxes, not headers or TODO comments
            if task_info['type'] != 'checkbox':
                continue
            
            line_num = task_info['line'] - 1  # 0-indexed
            if line_num < len(lines):
                original_line = lines[line_num]
                # Only update if it's actually an unchecked checkbox
                if '[ ]' in original_line:
                    # Replace [ ] with [x] (preserve spacing)
                    new_line = original_line.replace('[ ]', '[x]', 1)
                    if new_line != original_line:
                        lines[line_num] = new_line
                        modified = True
                        updated_count += 1
        
        if modified:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            return updated_count
    except Exception as e:
        print(f"Error updating {filepath}: {e}")
    return 0

def main():
    # Load validation results
    with open('task_validation_results.json', 'r') as f:
        validated_tasks = json.load(f)
    
    # Group tasks by file
    tasks_by_file = {}
    for task_data in validated_tasks:
        if task_data['status'] == 'likely_complete':
            filepath = task_data['task']['file']
            if filepath not in tasks_by_file:
                tasks_by_file[filepath] = []
            tasks_by_file[filepath].append(task_data['task'])
    
    # Update each file
    print(f"Updating {len(tasks_by_file)} files with completed tasks...")
    updated_files = 0
    total_checked = 0
    for filepath, tasks in tasks_by_file.items():
        count = update_file_checkboxes(filepath, tasks)
        if count > 0:
            updated_files += 1
            total_checked += count
            print(f"  ✓ Updated {filepath} ({count} checkboxes)")
    
    print(f"\n✅ Updated {updated_files} files")
    print(f"✅ Checked off {total_checked} completed tasks")

if __name__ == '__main__':
    main()
