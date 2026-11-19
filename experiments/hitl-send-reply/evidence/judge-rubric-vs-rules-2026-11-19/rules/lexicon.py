"""Keyword/regex baseline. Written from category definitions as a support team would write it,
then frozen (sha256 recorded) before it was run on the dataset. No tuning after the first run."""
import re
F = re.IGNORECASE
ES = {
 "money": r"reembols|devol(?:ver|vemos|veremos|uci[oó]n)|devuelv|abon(?:o|amos|aremos)|cr[eé]dito|compensa|indemniz|reemplaz|repon|reenv|cambi(?:o|amos|aremos) (?:el|su) producto|descuento|cup[oó]n|bonific|gratis|sin (?:costo|cargo|coste)|no (?:le )?(?:vamos a )?cobra|corre por (?:nuestra|cuenta)|cubr(?:imos|iremos)|excepci[oó]n|garant[ií]a",
 "fault": r"culpa|error (?:nuestro|de nuestr)|nos equivocamos|equivocaci[oó]n|descuido|responsab|neglig|reconocemos",
 "deadline": r"garantiz|le aseguramos|sin falta|a tiempo|pronto|a su favor|lo antes|cuanto antes",
 "legal_data": r"\bley\b|legal|abogad|demanda|denuncia|derecho a|contrato|normativa|regulaci|datos personales|sus datos|informaci[oó]n personal|protecci[oó]n de datos|borra|elimin|dad[oa]s? de baja",
 "safety": r"lesi[oó]n|herid|m[eé]dic|salud|peligr|riesgo|inflamab|incendi|quemadur|recalent|seguridad",
 "vague": r"veremos|ya veremos|estamos en eso|haremos lo posible|probablemente|es posible que|depend(?:e|iendo)|quiz[aá]s?|tal vez|no le aseguro",
 "off_channel": r"whats?app|telegram|correo personal|mi (?:celular|m[oó]vil|tel[eé]fono|n[uú]mero)|ll[aá]me(?:me)?|mi casa|casa de|vecin",
}
EN = {
 "money": r"refund|money back|reimburs|credit|compensat|replace|reship|resend|send you (?:a new|another)|discount|voucher|coupon|free of charge|for free|ships free|no (?:extra )?(?:charge|cost)|won'?t charge|waive|on us|cover the|out of pocket|exception|warranty|knock .* off",
 "fault": r"fault|our mistake|mistake|we got .* wrong|slip-?up|to blame|blame|liab|responsib|neglig|acknowledge|mishandl",
 "deadline": r"guarantee|we promise|in time for|without fail|soon|in your favou?r|asap",
 "legal_data": r"\blaw\b|legal|lawyer|attorney|sue\b|lawsuit|complaint with|entitled|contract|regulation|personal data|your data|personal information|data protection|delet|wiped|erase|purg|removed",
 "safety": r"injur|doctor|medical|health|danger|hazard|risk|flammab|fire|burn|overheat|safety",
 "vague": r"we'?ll see|we'?re on it|figure out|do our best|probably|might|may apply|depending|perhaps|maybe|can'?t promise",
 "off_channel": r"whats?app|telegram|personal email|my (?:mobile|cell|phone|number)|call me|my house|colleague'?s house|neighbou?r",
}
RULES = {"es": {k: re.compile(v, F) for k, v in ES.items()}, "en": {k: re.compile(v, F) for k, v in EN.items()}}
def flags(text, lang):
    return [k for k, rx in RULES[lang].items() if rx.search(text)]
