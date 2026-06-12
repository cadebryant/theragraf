import { jsPDF } from 'jspdf';
import type { CptCode, IcdCode, SoapNote } from '@/types';

export interface ExportData {
  clientId: string;
  sessionDate: string;   // row-key format: yyyy-MM-ddTHH-mm-ssZ
  therapistName: string;
  discipline: string;
  setting: string;
  payer: string;
  sessionDurationMinutes?: number | null;
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

  // ── SOAP Note ─────────────────────────────────────────────────────────────
  const soapSections: [string, string][] = [
    ['S — Subjective',  data.soapNote.subjective],
    ['O — Objective',   data.soapNote.objective],
    ['A — Assessment',  data.soapNote.assessment],
    ['P — Plan',        data.soapNote.plan],
  ];

  for (const [heading, body] of soapSections) {
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
    // Column headers
    doc.setFontSize(9);
    doc.setFont('helvetica', 'bold');
    doc.text('Code',        margin,        y);
    doc.text('Description', margin + 60,   y);
    doc.text('Units',       margin + 360,  y);
    doc.text('Rationale',   margin + 410,  y);
    y += lineH;
    doc.setDrawColor(200, 200, 200);
    doc.line(margin, y, pageWidth - margin, y);
    y += 4;

    doc.setFont('helvetica', 'normal');
    for (const cpt of data.cptCodes) {
      const descLines = doc.splitTextToSize(cpt.description, 290) as string[];
      const ratLines  = doc.splitTextToSize(cpt.rationale ?? '', 155) as string[];
      const rowLines  = Math.max(descLines.length, ratLines.length, 1);

      doc.text(cpt.code,                  margin,       y);
      doc.text(descLines,                 margin + 60,  y);
      doc.text(String(cpt.billableUnits), margin + 360, y);
      doc.text(ratLines,                  margin + 410, y);
      y += rowLines * lineH + 4;

      if (y > doc.internal.pageSize.getHeight() - margin * 2) {
        doc.addPage();
        y = margin;
      }
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
    doc.setFontSize(9);
    doc.setFont('helvetica', 'bold');
    doc.text('Code',        margin,       y);
    doc.text('Description', margin + 60,  y);
    doc.text('Rationale',   margin + 310, y);
    y += lineH;
    doc.line(margin, y, pageWidth - margin, y);
    y += 4;

    doc.setFont('helvetica', 'normal');
    for (const icd of data.icdCodes) {
      const descLines = doc.splitTextToSize(icd.description, 240) as string[];
      const ratLines  = doc.splitTextToSize(icd.rationale ?? '', 200) as string[];
      const rowLines  = Math.max(descLines.length, ratLines.length, 1);

      doc.text(icd.code,    margin,       y);
      doc.text(descLines,   margin + 60,  y);
      doc.text(ratLines,    margin + 310, y);
      y += rowLines * lineH + 4;

      if (y > doc.internal.pageSize.getHeight() - margin * 2) {
        doc.addPage();
        y = margin;
      }
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
