import type { CptCode, IcdCode } from '@/types';

export interface Export837pData {
  clientId: string;
  sessionDate: string;   // row-key format: yyyy-MM-ddTHH-mm-ssZ
  therapistName: string;
  discipline: string;
  payer: string;
  sessionDurationMinutes?: number | null;
  cptCodes: CptCode[];
  icdCodes: IcdCode[];
}

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Right-pad a string to exactly `len` characters with spaces. */
const pad = (s: string, len: number): string => s.substring(0, len).padEnd(len, ' ');

/** Zero-pad a number to `len` digits. */
const zeroPad = (n: number, len: number): string => String(n).padStart(len, '0');

/** Format a JS Date as YYYYMMDD. */
function yyyymmdd(d: Date): string {
  return [
    d.getUTCFullYear(),
    zeroPad(d.getUTCMonth() + 1, 2),
    zeroPad(d.getUTCDate(), 2),
  ].join('');
}

/** Format a JS Date as HHMM. */
function hhmm(d: Date): string {
  return zeroPad(d.getUTCHours(), 2) + zeroPad(d.getUTCMinutes(), 2);
}

/** Convert the row-key date format (yyyy-MM-ddTHH-mm-ssZ) to a JS Date. */
function rowKeyToDate(key: string): Date {
  return new Date(key.replace(/T(\d{2})-(\d{2})-(\d{2})Z$/, 'T$1:$2:$3Z'));
}

/**
 * Maps the app's discipline strings to X12 837P specialty codes.
 * These are approximate — update as needed for your clearinghouse.
 */
function disciplineToTaxonomy(discipline: string): string {
  switch (discipline) {
    case 'OccupationalTherapy':      return '225X00000X';
    case 'PhysicalTherapy':          return '225100000X';
    case 'SpeechLanguagePathology':  return '235Z00000X';
    case 'Psychotherapy':            return '101YA0400X';
    default:                         return '225X00000X';
  }
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * Builds a minimal but structurally valid X12 837P (Professional) EDI string
 * for a single therapy session claim and triggers a browser download.
 *
 * IMPORTANT: The following placeholder values MUST be replaced before
 * submitting to a clearinghouse or payer:
 *   PROVIDER_NPI       — billing provider NPI (10 digits)
 *   PROVIDER_TAX_ID    — billing provider tax ID / EIN (9 digits, no hyphens)
 *   PROVIDER_LAST_NAME — billing provider last name
 *   PROVIDER_FIRST     — billing provider first name
 *   PAYER_ID           — payer's electronic payer ID (from clearinghouse lookup)
 *   PAYER_NAME         — payer's full name
 */
export function exportSession837p(data: Export837pData): void {
  const now      = new Date();
  const svcDate  = rowKeyToDate(data.sessionDate);
  const svcDateStr = yyyymmdd(svcDate);

  const ISA_DATE  = yyyymmdd(now).slice(2);   // YYMMDD
  const ISA_TIME  = hhmm(now);
  const ctrlNum   = zeroPad(now.getTime() % 1_000_000_000, 9); // 9-digit interchange ctrl
  const gsCtrl    = ctrlNum.slice(-4);          // 4-digit group ctrl
  const stCtrl    = '0001';

  // Segment terminator and element separator follow X12 convention
  const SEG = '~\n';   // segment terminator  (~ + newline for readability)
  const SEP = '*';     // element separator
  const SUB = ':';     // sub-element separator (ISA16)

  const clmAmt = data.cptCodes
    .reduce((sum, c) => sum + c.billableUnits * 15, 0)  // placeholder unit value
    .toFixed(2);

  const segments: string[] = [];

  // ── ISA — Interchange Control Header ─────────────────────────────────────
  segments.push([
    'ISA',
    '00', pad('', 10),          // ISA01-02: auth info qualifier + auth info
    '00', pad('', 10),          // ISA03-04: security info qualifier + security info
    'ZZ', pad('THERAGRAF', 15), // ISA05-06: sender qualifier + sender ID
    'ZZ', pad('PAYER_ID', 15),  // ISA07-08: receiver qualifier + receiver ID
    ISA_DATE, ISA_TIME,         // ISA09-10: date, time
    '^',                        // ISA11: repetition separator
    '00501',                    // ISA12: version
    ctrlNum,                    // ISA13: interchange control number
    '0',                        // ISA14: acknowledgment requested
    'P',                        // ISA15: usage (P=production, T=test)
    SUB,                        // ISA16: sub-element separator
  ].join(SEP) + SEG);

  // ── GS — Functional Group Header ─────────────────────────────────────────
  segments.push([
    'GS',
    'HC',                       // GS01: functional identifier (HC = health care claim)
    'THERAGRAF',                // GS02: application sender code
    'PAYER_ID',                 // GS03: application receiver code
    yyyymmdd(now),              // GS04: date
    hhmm(now),                  // GS05: time
    gsCtrl,                     // GS06: group control number
    'X',                        // GS07: responsible agency code
    '005010X222A1',             // GS08: version/release
  ].join(SEP) + SEG);

  // ── ST — Transaction Set Header ───────────────────────────────────────────
  segments.push(['ST', '837', stCtrl, '005010X222A1'].join(SEP) + SEG);

  // ── BHT — Beginning of Hierarchical Transaction ──────────────────────────
  segments.push([
    'BHT',
    '0019',           // BHT01: hierarchical structure code (0019 = claim)
    '00',             // BHT02: transaction set purpose (00 = original)
    ctrlNum,          // BHT03: reference identification
    yyyymmdd(now),    // BHT04: date
    hhmm(now),        // BHT05: time
    'CH',             // BHT06: transaction type (CH = chargeable)
  ].join(SEP) + SEG);

  // ── Loop 1000A — Submitter (Billing Provider) ────────────────────────────
  let hlCount = 1;

  segments.push(['NM1', '41', '2', 'THERAGRAF', '', '', '', '', '46', 'THERAGRAF'].join(SEP) + SEG);
  segments.push(['PER', 'IC', 'Billing Contact', 'EM', 'billing@theragraf.app'].join(SEP) + SEG);

  // ── Loop 1000B — Receiver (Payer) ────────────────────────────────────────
  segments.push(['NM1', '40', '2', 'PAYER_NAME', '', '', '', '', 'XV', 'PAYER_ID'].join(SEP) + SEG);

  // ── HL — Billing Provider Hierarchical Level ─────────────────────────────
  segments.push(['HL', String(hlCount++), '', '20', '1'].join(SEP) + SEG);

  // PRV — Billing Provider Specialty
  segments.push(['PRV', 'BI', 'PXC', disciplineToTaxonomy(data.discipline)].join(SEP) + SEG);

  // NM1 — Billing Provider Name
  segments.push([
    'NM1', '85', '1',
    'PROVIDER_LAST_NAME', 'PROVIDER_FIRST', '', '', '',
    'XX', 'PROVIDER_NPI',
  ].join(SEP) + SEG);
  segments.push(['N3', '123 CLINIC ADDRESS ST'].join(SEP) + SEG);
  segments.push(['N4', 'CITY', 'ST', '00000'].join(SEP) + SEG);
  segments.push(['REF', 'EI', 'PROVIDER_TAX_ID'].join(SEP) + SEG);

  // ── HL — Subscriber Hierarchical Level ───────────────────────────────────
  segments.push(['HL', String(hlCount++), '1', '22', '0'].join(SEP) + SEG);
  segments.push(['SBR', 'P', '18', '', '', '', '', '', '', data.payer.toUpperCase()].join(SEP) + SEG);

  // NM1 — Subscriber (patient = client)
  segments.push([
    'NM1', 'IL', '1',
    data.clientId, '', '', '', '',
    'MI', data.clientId,          // member ID placeholder — same as clientId
  ].join(SEP) + SEG);

  // NM1 — Payer
  segments.push(['NM1', 'PR', '2', 'PAYER_NAME', '', '', '', '', 'XV', 'PAYER_ID'].join(SEP) + SEG);

  // ── CLM — Claim Information ───────────────────────────────────────────────
  segments.push([
    'CLM',
    `${data.clientId}-${data.sessionDate}`,   // CLM01: claim ID (unique per claim)
    clmAmt,                                   // CLM02: total charge
    '', '',                                   // CLM03-04: reserved
    '11:B:1',                                 // CLM05: place of service:facility:claim freq
    'Y',                                      // CLM06: provider/supplier signature
    'A',                                      // CLM07: assignment of benefits
    'Y',                                      // CLM08: release of information
    'I',                                      // CLM09: patient signature source
  ].join(SEP) + SEG);

  // DX — Diagnosis Codes (HI segment, ICD-10-CM)
  if (data.icdCodes.length > 0) {
    const dxElements = data.icdCodes
      .slice(0, 12)           // 837P supports max 12 diagnosis pointers
      .map(icd => `ABK${SEP}${icd.code.replace('.', '')}`);
    segments.push(['HI', ...dxElements].join(SEP) + SEG);
  }

  // ── SV1 — Professional Service Lines (one per CPT code) ──────────────────
  let lineNum = 1;
  for (const cpt of data.cptCodes) {
    // Build ordered diagnosis pointers (1-based index into ICD list, max 4)
    const dxPointers = data.icdCodes
      .slice(0, Math.min(4, data.icdCodes.length))
      .map((_, i) => String(i + 1))
      .join(SUB);

    segments.push([
      'LX', String(lineNum++),
    ].join(SEP) + SEG);

    segments.push([
      'SV1',
      `HC${SEP}${cpt.code}`,          // SV101: procedure code qualifier + code
      (cpt.billableUnits * 15).toFixed(2), // SV102: line charge (placeholder rate)
      'UN',                            // SV103: unit of measure
      String(cpt.billableUnits),       // SV104: quantity
      '',                              // SV105: facility code
      '',                              // SV106: service type code
      dxPointers,                      // SV107: diagnosis code pointers
    ].join(SEP) + SEG);

    // DTP — Service Date
    segments.push(['DTP', '472', 'D8', svcDateStr].join(SEP) + SEG);
  }

  // ── SE — Transaction Set Trailer ─────────────────────────────────────────
  const segmentCount = segments.length + 1; // +1 for SE itself
  segments.push(['SE', String(segmentCount), stCtrl].join(SEP) + SEG);

  // ── GE — Functional Group Trailer ────────────────────────────────────────
  segments.push(['GE', '1', gsCtrl].join(SEP) + SEG);

  // ── IEA — Interchange Control Trailer ────────────────────────────────────
  segments.push(['IEA', '1', ctrlNum].join(SEP) + SEG);

  // ── Download ──────────────────────────────────────────────────────────────
  const content  = segments.join('');
  const blob     = new Blob([content], { type: 'text/plain;charset=utf-8' });
  const url      = URL.createObjectURL(blob);
  const anchor   = document.createElement('a');
  anchor.href    = url;
  anchor.download = `claim_${data.clientId}_${data.sessionDate}.837p.edi`;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(url);
}
