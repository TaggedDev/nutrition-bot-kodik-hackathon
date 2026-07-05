"""
Extract Codex chat history related to the Nutrition project.
"""
import json
import os
from datetime import datetime, timezone

CODEX_HOME = os.path.expanduser("~/.codex")
HISTORY_FILE = os.path.join(CODEX_HOME, "history.jsonl")
SESSION_INDEX = os.path.join(CODEX_HOME, "session_index.jsonl")
OUTPUT_FILE = os.path.join(os.path.dirname(__file__), "..", "docs", "codex_chat_exports.json")

# Keywords to filter nutrition-related entries
KEYWORDS = [
    "nutrition", "d:\\code\\csharp\\nutrition", "openfoodfacts",
    "kbju", "нутрици", "калори", "calorie", "meal",
    "прием пищи", "продукт", "productname", "nutritionfacts",
    "off:", "compose.dev.yml", "docker compose",
]

# Load session index
sessions = {}
with open(SESSION_INDEX, "r", encoding="utf-8") as f:
    for line in f:
        try:
            d = json.loads(line)
            sessions[d["id"]] = d
        except:
            pass

# Scan history for Nutrition-related entries
nutrition_entries = []
seen_session_ids = set()

with open(HISTORY_FILE, "r", encoding="utf-8") as f:
    for i, line in enumerate(f):
        try:
            entry = json.loads(line)
            text_lower = json.dumps(entry).lower()
            
            # Check if this entry relates to Nutrition
            is_related = any(kw in text_lower for kw in KEYWORDS)
            
            if is_related:
                sid = entry.get("session_id", "unknown")
                session_info = sessions.get(sid, {})
                
                nutrition_entries.append({
                    "line_number": i,
                    "session_id": sid,
                    "session_name": session_info.get("thread_name", "?"),
                    "session_updated": session_info.get("updated_at", "?"),
                    "timestamp": entry.get("ts", 0),
                    "text": entry.get("text", "")[:500],
                })
                seen_session_ids.add(sid)
        except:
            pass

# Group by session
sessions_output = {}
for entry in nutrition_entries:
    sid = entry["session_id"]
    if sid not in sessions_output:
        sessions_output[sid] = {
            "session_name": entry["session_name"],
            "session_updated": entry["session_updated"],
            "messages": [],
        }
    sessions_output[sid]["messages"].append({
        "timestamp_unix": entry["timestamp"],
        "timestamp_iso": datetime.fromtimestamp(entry["timestamp"], tz=timezone.utc).isoformat() if entry["timestamp"] else "?",
        "text": entry["text"],
    })

# Write output
with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
    json.dump({
        "extracted_at": datetime.now(timezone.utc).isoformat(),
        "total_entries_found": len(nutrition_entries),
        "unique_sessions": len(sessions_output),
        "sessions": sessions_output,
    }, f, ensure_ascii=False, indent=2)

print(f"Found {len(nutrition_entries)} nutrition-related entries across {len(sessions_output)} sessions")
print(f"Output written to: {OUTPUT_FILE}")

# Print session summary
for sid, data in sessions_output.items():
    name = data["session_name"]
    msg_count = len(data["messages"])
    print(f"  Session {sid[:20]}... | '{name}' | {msg_count} messages")
