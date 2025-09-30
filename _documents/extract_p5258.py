from pdfminer.high_level import extract_text
from pathlib import Path
path = Path(r"_documents/p5258.pdf")
text = extract_text(path)
Path(r"_documents/p5258.txt").write_text(text, encoding="utf-8")
