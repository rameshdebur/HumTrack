# Hash mismatch fixture

The automated test copies the valid synthetic package and changes one listed
artifact without regenerating its SHA-256 index. The resulting temporary package
is deliberately invalid.
