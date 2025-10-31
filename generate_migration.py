#!/usr/bin/env python3
"""
Generate SQL migration from JSON difficulty files
Run this to create the INSERT statements for question_difficulties table
"""

import json
import os

def generate_migration():
    # Default success rates for initial import
    default_rates = {
        'easy': 70.00,
        'medium': 50.00,
        'hard': 30.00
    }
    
    sql_parts = []
    sql_parts.append("-- Auto-generated migration from JSON difficulty files")
    sql_parts.append("-- Insert all questions with their difficulty levels\n")
    sql_parts.append("INSERT INTO question_difficulties (\"QuestionFile\", \"Difficulty\", \"SuccessRate\", \"TotalAttempts\", \"CorrectAttempts\", \"ManualOverride\")")
    sql_parts.append("VALUES")
    
    values = []
    
    # Process each difficulty level
    for difficulty in ['easy', 'medium', 'hard']:
        json_file = f'wwwroot/difficulty/{difficulty}.json'
        
        if not os.path.exists(json_file):
            print(f"Warning: {json_file} not found")
            continue
        
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
            questions = data.get('questions', [])
            
            print(f"Processing {difficulty}: {len(questions)} questions")
            
            for question in questions:
                if question and question.strip():
                    # Escape single quotes in SQL
                    safe_question = question.replace("'", "''")
                    rate = default_rates[difficulty]
                    values.append(f"  ('{safe_question}', '{difficulty}', {rate}, 0, 0, true)")
    
    sql_parts.append(",\n".join(values))
    sql_parts.append("\nON CONFLICT (\"QuestionFile\") DO NOTHING;")
    sql_parts.append("\n\n-- Summary:")
    sql_parts.append(f"-- Total questions to insert: {len(values)}")
    
    # Write to file
    output_file = 'migration_generated.sql'
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write('\n'.join(sql_parts))
    
    print(f"\n[OK] Generated {output_file} with {len(values)} questions")
    print(f"[INFO] Run this file in Supabase SQL Editor to populate the database")

if __name__ == '__main__':
    generate_migration()

