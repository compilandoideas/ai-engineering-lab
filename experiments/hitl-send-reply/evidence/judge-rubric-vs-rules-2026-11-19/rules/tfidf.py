import json
import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import StratifiedKFold, cross_val_predict
from sklearn.pipeline import make_pipeline, make_union
base = "../../../src/backend/HitlSendReply.Evals/data"
for lang in ("es","en"):
    ids = {json.loads(l)["id"] for l in open(f"../../judge-study-2026-11-19/raw/{lang}-gpt-4.1-mini-base.jsonl")}
    rows = [r for r in (json.loads(l) for l in open(f"{base}/drafts.{lang}.jsonl", encoding="utf-8")) if r["id"] in ids]
    X = [r["text"] for r in rows]; y = np.array([r["expected"]=="NeedsHuman" for r in rows])
    esc_all, fp_all = [], []
    for seed in range(5):
        pipe = make_pipeline(make_union(TfidfVectorizer(ngram_range=(1,2), sublinear_tf=True),
                                        TfidfVectorizer(analyzer="char_wb", ngram_range=(3,5), sublinear_tf=True)),
                             LogisticRegression(C=10, class_weight="balanced", max_iter=2000))
        p = cross_val_predict(pipe, X, y, cv=StratifiedKFold(5, shuffle=True, random_state=seed), method="predict_proba")[:,1]
        # Threshold chosen for recall, as a guardrail would: escalate if p >= 0.3
        pred = p >= 0.3
        esc_all.append(int(((y) & (~pred)).sum())); fp_all.append(int(((~y) & pred).sum()))
    print(f"{lang}: 5x 5-fold CV, threshold 0.3 -> escaped {esc_all} of 60, FP {fp_all} of 30")
