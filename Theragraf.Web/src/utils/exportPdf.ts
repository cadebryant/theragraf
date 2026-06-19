import { jsPDF } from 'jspdf';
import type { CptCode, IcdCode, NoteFormat, SoapNote } from '@/types';

export interface ExportData {
  clientId: string;
  sessionDate: string;   // row-key format: yyyy-MM-ddTHH-mm-ssZ
  therapistName: string;
  discipline: string;
  setting: string;
  payer: string;
  sessionDurationMinutes?: number | null;
  noteFormat?: NoteFormat;
  soapNote: SoapNote;
  cptCodes: CptCode[];
  icdCodes: IcdCode[];
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function rowKeyToDisplay(key: string): string {
  const iso = key.replace(/T(\d{2})-(\d{2})-(\d{2})Z$/, 'T$1:$2:$3Z');
  return new Date(iso).toLocaleString();
}

/** Wraps text at maxWidth and returns the number of lines printed. */
function printWrapped(
  doc: jsPDF,
  text: string,
  x: number,
  y: number,
  maxWidth: number,
): number {
  const lines = doc.splitTextToSize(text || '—', maxWidth) as string[];
  doc.text(lines, x, y);
  return lines.length;
}

// ── Public API ────────────────────────────────────────────────────────────────

/**
 * Generates a human-readable PDF for a completed therapy session and
 * triggers a browser download.
 */
export function exportSessionPdf(data: ExportData): void {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'letter' });

  const margin = 50;
  const pageWidth = doc.internal.pageSize.getWidth();
  const contentWidth = pageWidth - margin * 2;
  const lineH = 14;
  let y = margin;

  // ── Header ────────────────────────────────────────────────────────────────
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(18);
  doc.text('Theragraf — Session Documentation', margin, y);
  y += lineH * 1.8;

  doc.setDrawColor(180, 180, 180);
  doc.line(margin, y, pageWidth - margin, y);
  y += lineH;

  // ── Session metadata ──────────────────────────────────────────────────────
  doc.setFontSize(10);
  const meta: [string, string][] = [
    ['Client ID',    data.clientId],
    ['Session Date', rowKeyToDisplay(data.sessionDate)],
    ['Therapist',    data.therapistName],
    ['Discipline',   data.discipline],
    ['Setting',      data.setting],
    ['Payer',        data.payer],
    ['Duration',     data.sessionDurationMinutes ? `${data.sessionDurationMinutes} min` : '—'],
  ];

  for (const [label, value] of meta) {
    doc.setFont('helvetica', 'bold');
    doc.text(`${label}:`, margin, y);
    doc.setFont('helvetica', 'normal');
    doc.text(value, margin + 100, y);
    y += lineH * 1.3;
  }

  y += lineH * 0.5;
  doc.line(margin, y, pageWidth - margin, y);
  y += lineH * 1.2;

  // ── Note title + sections (SOAP or DAP) ─────────────────────────────────
  const isDap = data.noteFormat === 'Dap';
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(12);
  doc.text(isDap ? 'DAP Note' : 'SOAP Note', margin, y);
  y += lineH * 1.5;

  const noteSections: [string, string][] = isDap
    ? [
        ['D — Data',       data.soapNote.subjective],
        ['A — Assessment', data.soapNote.assessment],
        ['P — Plan',       data.soapNote.plan],
      ]
    : [
        ['S — Subjective', data.soapNote.subjective],
        ['O — Objective',  data.soapNote.objective],
        ['A — Assessment', data.soapNote.assessment],
        ['P — Plan',       data.soapNote.plan],
      ];

  for (const [heading, body] of noteSections) {
    // Section heading
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text(heading, margin, y);
    y += lineH * 1.2;

    // Body text
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    const linesUsed = printWrapped(doc, body, margin, y, contentWidth);
    y += linesUsed * lineH + lineH * 0.8;

    // Page break guard
    if (y > doc.internal.pageSize.getHeight() - margin * 2) {
      doc.addPage();
      y = margin;
    }
  }

  doc.line(margin, y, pageWidth - margin, y);
  y += lineH * 1.2;

  // ── CPT Codes ─────────────────────────────────────────────────────────────
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  doc.text('CPT Codes', margin, y);
  y += lineH * 1.2;

  if (data.cptCodes.length === 0) {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    doc.text('None', margin, y);
    y += lineH * 1.5;
  } else {
    // Column definitions with proper widths that fit the page
    const cptCodeX = margin;
    const cptCodeWidth = 50;
    const cptDescX = cptCodeX + cptCodeWidth + 10;
    const cptDescWidth = 200;
    const cptUnitsX = cptDescX + cptDescWidth + 10;
    const cptUnitsWidth = 35;
    const cptRatX = cptUnitsX + cptUnitsWidth + 10;
    const cptRatWidth = contentWidth - (cptRatX - margin);  // Use remaining space

    // Column headers
    doc.setFontSize(9);
    doc.setFont('helvetica', 'bold');
    doc.text('Code',        cptCodeX, y);
    doc.text('Description', cptDescX, y);
    doc.text('Units',       cptUnitsX, y);
    doc.text('Rationale',   cptRatX, y);
    y += lineH;
    doc.setDrawColor(200, 200, 200);
    doc.line(margin, y, pageWidth - margin, y);
    y += 4;

    doc.setFont('helvetica', 'normal');
    for (const cpt of data.cptCodes) {
      const descLines = doc.splitTextToSize(cpt.description, cptDescWidth) as string[];
      const ratLines  = doc.splitTextToSize(cpt.rationale ?? '', cptRatWidth) as string[];
      const rowLines  = Math.max(descLines.length, ratLines.length, 1);

      // Check if we need a new page BEFORE printing
      if (y + (rowLines * lineH) > doc.internal.pageSize.getHeight() - margin * 2) {
        doc.addPage();
        y = margin;
      }

      doc.text(cpt.code,                  cptCodeX,  y);
      doc.text(descLines,                 cptDescX,  y);
      doc.text(String(cpt.billableUnits), cptUnitsX, y);
      doc.text(ratLines,                  cptRatX,   y);
      y += rowLines * lineH + 6;  // Add more vertical spacing between rows
    }
  }

  y += lineH * 0.5;
  doc.line(margin, y, pageWidth - margin, y);
  y += lineH * 1.2;

  // ── ICD-10 Codes ──────────────────────────────────────────────────────────
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  doc.text('ICD-10 Diagnosis Codes', margin, y);
  y += lineH * 1.2;

  if (data.icdCodes.length === 0) {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    doc.text('None', margin, y);
  } else {
    // Column definitions with proper widths
    const icdCodeX = margin;
    const icdCodeWidth = 50;
    const icdDescX = icdCodeX + icdCodeWidth + 10;
    const icdDescWidth = 220;
    const icdRatX = icdDescX + icdDescWidth + 10;
    const icdRatWidth = contentWidth - (icdRatX - margin);  // Use remaining space

    doc.setFontSize(9);
    doc.setFont('helvetica', 'bold');
    doc.text('Code',        icdCodeX, y);
    doc.text('Description', icdDescX, y);
    doc.text('Rationale',   icdRatX,  y);
    y += lineH;
    doc.line(margin, y, pageWidth - margin, y);
    y += 4;

    doc.setFont('helvetica', 'normal');
    for (const icd of data.icdCodes) {
      const descLines = doc.splitTextToSize(icd.description, icdDescWidth) as string[];
      const ratLines  = doc.splitTextToSize(icd.rationale ?? '', icdRatWidth) as string[];
      const rowLines  = Math.max(descLines.length, ratLines.length, 1);

      // Check if we need a new page BEFORE printing
      if (y + (rowLines * lineH) > doc.internal.pageSize.getHeight() - margin * 2) {
        doc.addPage();
        y = margin;
      }

      doc.text(icd.code,    icdCodeX, y);
      doc.text(descLines,   icdDescX, y);
      doc.text(ratLines,    icdRatX,  y);
      y += rowLines * lineH + 6;  // Add more vertical spacing between rows
    }
  }

  // ── Footer ────────────────────────────────────────────────────────────────
  const footerY = doc.internal.pageSize.getHeight() - 30;
  doc.setFont('helvetica', 'italic');
  doc.setFontSize(8);
  doc.setTextColor(150);
  doc.text(
    `Generated by Theragraf on ${new Date().toLocaleString()}`,
    margin,
    footerY,
  );

  // ── Save ──────────────────────────────────────────────────────────────────
  const filename = `session_${data.clientId}_${data.sessionDate}.pdf`;
  doc.save(filename);
}
