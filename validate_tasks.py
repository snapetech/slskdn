#!/usr/bin/env python3
"""
Validate unchecked tasks in markdown files.
Checks git commits and code existence for each task.
"""
import os
import re
import subprocess
import json
from pathlib import Path
from typing import List, Dict, Optional

def run_git_command(cmd: List[str]) -> str:
    """Run a git command and return output"""
    try:
        result = subprocess.run(
            ['git'] + cmd,
            capture_output=True,
            text=True,
            cwd=os.getcwd(),
            timeout=30
        )
        return result.stdout.strip()
    except Exception as e:
        return f"ERROR: {e}"

def find_all_md_files() -> List[str]:
    """Find all markdown files in the repo"""
    md_files = []
    ignore_dirs = {'.git', 'node_modules', '__pycache__', '.venv', 'bin', 'obj', '.vs', '.idea'}
    
    for root, dirs, files in os.walk('.'):
        # Filter out ignored directories
        dirs[:] = [d for d in dirs if d not in ignore_dirs and not d.startswith('.')]
        for file in files:
            if file.endswith('.md'):
                full_path = os.path.join(root, file)
                md_files.append(full_path)
    return sorted(md_files)

def extract_task_id(text: str) -> Optional[str]:
    """Extract task ID from text (T-XXX, H-XXX, SF-XXX, etc.)"""
    patterns = [
        r'\b(T-\w+-\d+)\b',  # T-SF02, T-SF03, etc.
        r'\b(H-\d+)\b',       # H-08, H-09, etc.
        r'\b(SF-\d+)\b',      # SF-01, SF-02, etc.
        r'\b(TASK-\d+)\b',    # TASK-123
        r'\b(#[A-Z]+-\d+)\b', # #TASK-123
    ]
    for pattern in patterns:
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            return match.group(1).upper()
    return None

def check_git_commits(task_id: str) -> bool:
    """Check if task ID appears in git commit messages"""
    if not task_id:
        return False
    output = run_git_command(['log', '--all', '--grep', task_id, '--oneline'])
    return bool(output and 'ERROR' not in output)

def check_code_exists(task_text: str, filepath: str) -> Dict[str, bool]:
    """Check if code related to task exists in codebase"""
    results = {
        'has_implementation': False,
        'has_tests': False,
        'has_config': False
    }
    
    # Extract meaningful keywords (skip common words)
    stop_words = {'this', 'that', 'with', 'from', 'have', 'will', 'should', 'would', 'could', 
                  'implement', 'add', 'create', 'update', 'fix', 'remove', 'delete', 'change'}
    keywords = re.findall(r'\b[a-zA-Z]{5,}\b', task_text.lower())
    important_keywords = [kw for kw in keywords if kw not in stop_words and len(kw) > 4][:3]
    
    if not important_keywords:
        return results
    
    # Quick search using git grep (faster)
    try:
        for keyword in important_keywords:
            # Search in source files
            result = subprocess.run(
                ['git', 'grep', '-i', keyword, '--', '*.cs', '*.js', '*.jsx', '*.ts', '*.tsx'],
                capture_output=True,
                text=True,
                timeout=5,
                cwd=os.getcwd()
            )
            if result.returncode == 0 and result.stdout.strip():
                results['has_implementation'] = True
                break
    except:
        pass
    
    return results

def extract_unchecked_tasks(filepath: str) -> List[Dict]:
    """Extract unchecked checkboxes and task items from markdown"""
    tasks = []
    # Skip certain files that are just documentation/reference
    skip_patterns = ['CURSOR-WARNINGS', 'README', 'CONTRIBUTING', 'AGENTS.md']
    if any(pattern in filepath for pattern in skip_patterns):
        return tasks  # Skip these files - they're checklists, not tasks
    
    try:
        with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()
            for i, line in enumerate(lines, 1):
                # Unchecked checkbox: [ ] - only if it looks like a real task
                if re.search(r'^\s*[-*]\s*\[ \]', line) or re.search(r'^\s*\d+\.\s*\[ \]', line):
                    # Skip if it's just a checklist item (REJECT, WARNING patterns)
                    if re.search(r'\(REJECT|\(WARNING|\(ACCEPT', line):
                        continue
                    # Skip if it's too short or just a note
                    if len(line.strip()) < 20:
                        continue
                    task_id = extract_task_id(line)
                    tasks.append({
                        'file': filepath,
                        'line': i,
                        'text': line.strip(),
                        'type': 'checkbox',
                        'task_id': task_id
                    })
                # TODO/FIXME/XXX patterns (but not in checked boxes or false positives)
                elif re.search(r'\[x\]', line, re.IGNORECASE):
                    continue  # Skip checked items
                elif re.search(r'\b(TODO|FIXME|XXX|HACK|PLANNED|FUTURE)\b', line, re.IGNORECASE):
                    # Skip false positives
                    if any(x in line.lower() for x in ['todo.md', 'note that', 'project note', 'see note']):
                        continue
                    task_id = extract_task_id(line)
                    tasks.append({
                        'file': filepath,
                        'line': i,
                        'text': line.strip(),
                        'type': 'todo',
                        'task_id': task_id
                    })
    except Exception as e:
        pass  # Silently skip errors
    return tasks

def main():
    print("=" * 80)
    print("TASK VALIDATION SCRIPT")
    print("=" * 80)
    
    # Find all markdown files
    md_files = find_all_md_files()
    print(f"\nFound {len(md_files)} markdown files")
    
    # Extract all unchecked tasks
    print("\nScanning for unchecked tasks...")
    all_tasks = []
    for md_file in md_files:
        tasks = extract_unchecked_tasks(md_file)
        all_tasks.extend(tasks)
    
    print(f"Found {len(all_tasks)} unchecked tasks/todos")
    
    # Validate each task (with progress updates)
    print("\n" + "=" * 80)
    print("VALIDATING TASKS")
    print("=" * 80)
    
    validated_tasks = []
    total = len(all_tasks)
    for i, task in enumerate(all_tasks, 1):
        if i % 50 == 0:
            print(f"\nProgress: {i}/{total} ({i*100//total}%)")
        
        validation = {
            'task': task,
            'has_commit': False,
            'has_code': {'has_implementation': False, 'has_tests': False, 'has_config': False},
            'status': 'unknown'
        }
        
        # Check git commits
        if task['task_id']:
            validation['has_commit'] = check_git_commits(task['task_id'])
        
        # Check code existence
        code_check = check_code_exists(task['text'], task['file'])
        validation['has_code'] = code_check
        
        # Determine status
        if validation['has_commit'] or any(code_check.values()):
            validation['status'] = 'likely_complete'
        else:
            validation['status'] = 'pending'
        
        validated_tasks.append(validation)
    
    # Summary
    print("\n" + "=" * 80)
    print("SUMMARY")
    print("=" * 80)
    
    likely_complete = [t for t in validated_tasks if t['status'] == 'likely_complete']
    pending = [t for t in validated_tasks if t['status'] == 'pending']
    
    print(f"\nTotal tasks: {len(validated_tasks)}")
    print(f"Likely complete: {len(likely_complete)}")
    print(f"Pending: {len(pending)}")
    
    # Save results
    output_file = 'task_validation_results.json'
    with open(output_file, 'w') as f:
        json.dump(validated_tasks, f, indent=2)
    print(f"\nResults saved to: {output_file}")
    
    # Print likely complete tasks
    if likely_complete:
        print("\n" + "=" * 80)
        print("LIKELY COMPLETE TASKS (should be checked)")
        print("=" * 80)
        for task in likely_complete[:50]:  # Show first 50
            t = task['task']
            print(f"\n{t['file']}:{t['line']}")
            print(f"  {t['text'][:100]}")
            if task['has_commit']:
                print(f"  ✓ Found in commits")
            if any(task['has_code'].values()):
                print(f"  ✓ Code evidence: {[k for k, v in task['has_code'].items() if v]}")

if __name__ == '__main__':
    import sys
    main()
