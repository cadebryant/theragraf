import { useState } from 'react';
import {
  makeStyles,
  tokens,
  Card,
  Text,
  Button,
  Input,
  Field,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Tooltip,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import type { CptCode, IcdCode } from '@/types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  headerRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  addForm: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
    gap: tokens.spacingHorizontalS,
    alignItems: 'flex-end',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
  },
  rationalCell: {
    maxWidth: '400px',
    overflow: 'visible',
    wordBreak: 'break-word',
    whiteSpace: 'normal',
  },
});

// ── CPT Codes ─────────────────────────────────────────────────────────────────

interface CptCodesEditorProps {
  codes: CptCode[];
  onChange: (codes: CptCode[]) => void;
  readOnly?: boolean;
}

export function CptCodesEditor({ codes, onChange, readOnly = false }: CptCodesEditorProps) {
  const styles = useStyles();
  const [draft, setDraft] = useState<Partial<CptCode>>({});

  function addCode() {
    if (!draft.code?.trim()) return;
    onChange([
      ...codes,
      {
        code: draft.code.trim(),
        description: draft.description ?? '',
        rationale: draft.rationale ?? '',
        billableUnits: draft.billableUnits ?? 1,
      },
    ]);
    setDraft({});
  }

  function removeCode(index: number) {
    onChange(codes.filter((_, i) => i !== index));
  }

  function updateUnits(index: number, units: number) {
    onChange(codes.map((c, i) => (i === index ? { ...c, billableUnits: units } : c)));
  }

  return (
    <Card className={styles.card}>
      <div className={styles.headerRow}>
        <Text className={styles.title}>CPT Codes</Text>
      </div>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell style={{ width: 90 }}>Code</TableHeaderCell>
            <TableHeaderCell>Description</TableHeaderCell>
            <TableHeaderCell style={{ width: 80 }}>Units</TableHeaderCell>
            <TableHeaderCell>Rationale</TableHeaderCell>
            {!readOnly && <TableHeaderCell style={{ width: 60 }} />}
          </TableRow>
        </TableHeader>
        <TableBody>
          {codes.map((c, i) => (
            <TableRow key={i}>
              <TableCell>
                <Text weight="semibold">{c.code}</Text>
              </TableCell>
              <TableCell>{c.description}</TableCell>
              <TableCell>
                {readOnly ? (
                  c.billableUnits
                ) : (
                  <Input
                    type="number"
                    value={String(c.billableUnits)}
                    onChange={(_e, d) => updateUnits(i, parseInt(d.value, 10) || 1)}
                    style={{ width: '60px' }}
                    size="small"
                  />
                )}
              </TableCell>
              <TableCell>
                <Tooltip content={c.rationale} relationship="label">
                  <Text className={styles.rationalCell}>{c.rationale}</Text>
                </Tooltip>
              </TableCell>
              {!readOnly && (
                <TableCell>
                  <Button
                    size="small"
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => removeCode(i)}
                  />
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {!readOnly && (
        <div className={styles.addForm}>
          <Field label="Code">
            <Input
              value={draft.code ?? ''}
              onChange={(_e, d) => setDraft((p) => ({ ...p, code: d.value }))}
              placeholder="97110"
              size="small"
            />
          </Field>
          <Field label="Description">
            <Input
              value={draft.description ?? ''}
              onChange={(_e, d) => setDraft((p) => ({ ...p, description: d.value }))}
              placeholder="Therapeutic exercises"
              size="small"
            />
          </Field>
          <Field label="Units">
            <Input
              type="number"
              value={String(draft.billableUnits ?? 1)}
              onChange={(_e, d) =>
                setDraft((p) => ({ ...p, billableUnits: parseInt(d.value, 10) || 1 }))
              }
              style={{ width: '60px' }}
              size="small"
            />
          </Field>
          <Button icon={<Add24Regular />} onClick={addCode} size="small">
            Add Code
          </Button>
        </div>
      )}
    </Card>
  );
}

// ── ICD-10 Codes ──────────────────────────────────────────────────────────────

interface IcdCodesEditorProps {
  codes: IcdCode[];
  onChange: (codes: IcdCode[]) => void;
  readOnly?: boolean;
}

export function IcdCodesEditor({ codes, onChange, readOnly = false }: IcdCodesEditorProps) {
  const styles = useStyles();
  const [draft, setDraft] = useState<Partial<IcdCode>>({});

  function addCode() {
    if (!draft.code?.trim()) return;
    onChange([
      ...codes,
      {
        code: draft.code.trim(),
        description: draft.description ?? '',
        rationale: draft.rationale ?? '',
      },
    ]);
    setDraft({});
  }

  function removeCode(index: number) {
    onChange(codes.filter((_, i) => i !== index));
  }

  return (
    <Card className={styles.card}>
      <div className={styles.headerRow}>
        <Text className={styles.title}>ICD-10 Codes</Text>
      </div>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell style={{ width: 100 }}>Code</TableHeaderCell>
            <TableHeaderCell>Description</TableHeaderCell>
            <TableHeaderCell>Rationale</TableHeaderCell>
            {!readOnly && <TableHeaderCell style={{ width: 60 }} />}
          </TableRow>
        </TableHeader>
        <TableBody>
          {codes.map((c, i) => (
            <TableRow key={i}>
              <TableCell>
                <Text weight="semibold">{c.code}</Text>
              </TableCell>
              <TableCell>{c.description}</TableCell>
              <TableCell>
                <Tooltip content={c.rationale} relationship="label">
                  <Text className={styles.rationalCell}>{c.rationale}</Text>
                </Tooltip>
              </TableCell>
              {!readOnly && (
                <TableCell>
                  <Button
                    size="small"
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => removeCode(i)}
                  />
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {!readOnly && (
        <div className={styles.addForm}>
          <Field label="Code">
            <Input
              value={draft.code ?? ''}
              onChange={(_e, d) => setDraft((p) => ({ ...p, code: d.value }))}
              placeholder="M62.81"
              size="small"
            />
          </Field>
          <Field label="Description">
            <Input
              value={draft.description ?? ''}
              onChange={(_e, d) => setDraft((p) => ({ ...p, description: d.value }))}
              placeholder="Muscle weakness"
              size="small"
            />
          </Field>
          <Button icon={<Add24Regular />} onClick={addCode} size="small">
            Add Code
          </Button>
        </div>
      )}
    </Card>
  );
}
