import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Text,
  Button,
  Badge,
  Card,
  Spinner,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Field,
  Input,
  Textarea,
  Select,
  Checkbox,
  Accordion,
  AccordionItem,
  AccordionHeader,
  AccordionPanel,
  Tooltip,
} from '@fluentui/react-components';
import {
  Add24Regular,
  Checkmark24Regular,
  Delete24Regular,
  Edit24Regular,
  Sparkle24Regular,
  Warning24Regular,
  ChevronDown24Regular,
} from '@fluentui/react-icons';
import {
  createGoal,
  deleteGoal,
  getGoals,
  suggestGoals,
  updateGoal,
} from '@/api/goals';
import type {
  CreateGoalRequest,
  GoalResponse,
  GoalStatus,
  GoalSuggestion,
  SoapNote,
  TherapyDiscipline,
  UpdateGoalRequest,
} from '@/types';

// ── Styles ────────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  headerRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalM,
  },
  sectionTitle: {
    fontSize: tokens.fontSizeBase400,
    fontWeight: tokens.fontWeightSemibold,
  },
  toolbar: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  goalCard: {
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalM}`,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  goalCardHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  goalTitle: {
    fontWeight: tokens.fontWeightSemibold,
    flex: 1,
  },
  goalActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexShrink: 0,
  },
  goalMeta: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
  },
  progressNotes: {
    paddingLeft: tokens.spacingHorizontalM,
    borderLeft: `2px solid ${tokens.colorNeutralStroke2}`,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  progressNoteRow: {
    display: 'flex',
    flexDirection: 'column',
  },
  progressNoteDate: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
  },
  formGrid: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  suggestCard: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  suggestCardHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
});

// ── Helpers ───────────────────────────────────────────────────────────────────

const STATUS_LABELS: Record<GoalStatus, string> = {
  Active: 'Active',
  Met: 'Met ✓',
  Discontinued: 'Discontinued',
  NotMet: 'Not Met',
};

const STATUS_COLORS: Record<GoalStatus, 'brand' | 'success' | 'warning' | 'danger'> = {
  Active: 'brand',
  Met: 'success',
  Discontinued: 'warning',
  NotMet: 'danger',
};

function fmtDate(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString();
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface Props {
  clientId: string;
  /** When provided, enables the AI Suggest button. */
  latestSoapNote?: SoapNote;
  /** Used for AI suggestion prompting. */
  discipline?: TherapyDiscipline;
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function GoalsPanel({ clientId, latestSoapNote, discipline }: Props) {
  const styles = useStyles();
  const qc = useQueryClient();

  // ── Dialog state ──────────────────────────────────────────────────────────

  const [createOpen,   setCreateOpen]   = useState(false);
  const [editGoal,     setEditGoal]     = useState<GoalResponse | null>(null);
  const [progressGoal, setProgressGoal] = useState<GoalResponse | null>(null);
  const [suggestOpen,  setSuggestOpen]  = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<GoalResponse | null>(null);

  // ── Form state ────────────────────────────────────────────────────────────

  const [newTitle,      setNewTitle]      = useState('');
  const [newDesc,       setNewDesc]       = useState('');
  const [newTarget,     setNewTarget]     = useState('');
  const [editTitle,     setEditTitle]     = useState('');
  const [editDesc,      setEditDesc]      = useState('');
  const [editStatus,    setEditStatus]    = useState<GoalStatus>('Active');
  const [editTarget,    setEditTarget]    = useState('');
  const [progressNote,  setProgressNote]  = useState('');
  const [selectedSuggestions, setSelectedSuggestions] = useState<Set<number>>(new Set());

  // ── Data fetching ─────────────────────────────────────────────────────────

  const goalsQuery = useQuery({
    queryKey: ['goals', clientId],
    queryFn: () => getGoals(clientId),
    enabled: !!clientId,
  });

  const suggestQuery = useMutation({
    mutationFn: () =>
      suggestGoals(clientId, {
        soapNote: latestSoapNote!,
        discipline: discipline ?? 'OccupationalTherapy',
      }),
    onSuccess: () => setSuggestOpen(true),
  });

  const createMut = useMutation({
    mutationFn: (req: CreateGoalRequest) => createGoal(clientId, req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['goals', clientId] });
      setCreateOpen(false);
      setNewTitle(''); setNewDesc(''); setNewTarget('');
    },
  });

  const updateMut = useMutation({
    mutationFn: ({ goalId, req }: { goalId: string; req: UpdateGoalRequest }) =>
      updateGoal(clientId, goalId, req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['goals', clientId] });
      setEditGoal(null);
      setProgressGoal(null);
      setProgressNote('');
    },
  });

  const deleteMut = useMutation({
    mutationFn: (goalId: string) => deleteGoal(clientId, goalId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['goals', clientId] });
      setDeleteTarget(null);
    },
  });

  // ── Handlers ──────────────────────────────────────────────────────────────

  function openEdit(g: GoalResponse) {
    setEditGoal(g);
    setEditTitle(g.title);
    setEditDesc(g.description);
    setEditStatus(g.status);
    setEditTarget(g.targetDate ? g.targetDate.slice(0, 10) : '');
  }

  function submitCreate() {
    if (!newTitle.trim()) return;
    createMut.mutate({
      title: newTitle.trim(),
      description: newDesc.trim(),
      targetDate: newTarget ? new Date(newTarget).toISOString() : undefined,
    });
  }

  function submitEdit() {
    if (!editGoal) return;
    updateMut.mutate({
      goalId: editGoal.goalId,
      req: {
        title: editTitle.trim() || undefined,
        description: editDesc.trim() || undefined,
        status: editStatus,
        targetDate: editTarget ? new Date(editTarget).toISOString() : undefined,
      },
    });
  }

  function submitProgress() {
    if (!progressGoal || !progressNote.trim()) return;
    updateMut.mutate({
      goalId: progressGoal.goalId,
      req: { progressNote: progressNote.trim() },
    });
  }

  function markStatus(g: GoalResponse, status: GoalStatus) {
    updateMut.mutate({ goalId: g.goalId, req: { status } });
  }

  function acceptSuggestions(suggestions: GoalSuggestion[]) {
    const toAdd = suggestions.filter((_, i) => selectedSuggestions.has(i));
    Promise.all(toAdd.map((s) => createGoal(clientId, { title: s.title, description: s.description })))
      .then(() => {
        qc.invalidateQueries({ queryKey: ['goals', clientId] });
        setSuggestOpen(false);
        setSelectedSuggestions(new Set());
      });
  }

  // ── Render ────────────────────────────────────────────────────────────────

  const goals = goalsQuery.data ?? [];
  const active = goals.filter((g) => g.status === 'Active');
  const resolved = goals.filter((g) => g.status !== 'Active');

  return (
    <div className={styles.section}>
      {/* ── Header ── */}
      <div className={styles.headerRow}>
        <Text className={styles.sectionTitle}>
          Treatment Goals{goals.length > 0 ? ` (${goals.length})` : ''}
        </Text>
        <div className={styles.toolbar}>
          {latestSoapNote && (
            <Button
              appearance="subtle"
              icon={suggestQuery.isPending ? <Spinner size="tiny" /> : <Sparkle24Regular />}
              disabled={suggestQuery.isPending}
              onClick={() => suggestQuery.mutate()}
            >
              AI Suggest
            </Button>
          )}
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => setCreateOpen(true)}
          >
            Add Goal
          </Button>
        </div>
      </div>

      {goalsQuery.isLoading && <Spinner label="Loading goals…" />}

      {!goalsQuery.isLoading && goals.length === 0 && (
        <Text className={styles.emptyState}>
          No treatment goals yet. Use <strong>Add Goal</strong> to create one, or <strong>AI Suggest</strong> to generate goals from the latest session note.
        </Text>
      )}

      {/* ── Active goals ── */}
      {active.length > 0 && (
        <div className={styles.section}>
          {active.map((g) => (
            <GoalCard
              key={g.goalId}
              goal={g}
              styles={styles}
              onEdit={openEdit}
              onProgress={(goal) => { setProgressGoal(goal); setProgressNote(''); }}
              onMarkMet={(goal) => markStatus(goal, 'Met')}
              onDelete={setDeleteTarget}
              isUpdating={updateMut.isPending}
            />
          ))}
        </div>
      )}

      {/* ── Resolved goals (collapsible) ── */}
      {resolved.length > 0 && (
        <Accordion collapsible>
          <AccordionItem value="resolved">
            <AccordionHeader expandIcon={<ChevronDown24Regular />}>
              <Text>Resolved Goals ({resolved.length})</Text>
            </AccordionHeader>
            <AccordionPanel>
              <div className={styles.section} style={{ paddingTop: tokens.spacingVerticalS }}>
                {resolved.map((g) => (
                  <GoalCard
                    key={g.goalId}
                    goal={g}
                    styles={styles}
                    onEdit={openEdit}
                    onProgress={(goal) => { setProgressGoal(goal); setProgressNote(''); }}
                    onMarkMet={(goal) => markStatus(goal, 'Met')}
                    onDelete={setDeleteTarget}
                    isUpdating={updateMut.isPending}
                  />
                ))}
              </div>
            </AccordionPanel>
          </AccordionItem>
        </Accordion>
      )}

      {/* ── Create dialog ── */}
      <Dialog open={createOpen} onOpenChange={(_e, d) => setCreateOpen(d.open)}>
        <DialogSurface>
          <DialogTitle>Add Treatment Goal</DialogTitle>
          <DialogBody>
            <DialogContent>
              <div className={styles.formGrid}>
                <Field label="Title" required>
                  <Input
                    value={newTitle}
                    onChange={(_e, d) => setNewTitle(d.value)}
                    placeholder="e.g. Improve independent dressing"
                  />
                </Field>
                <Field label="Description (SMART)">
                  <Textarea
                    value={newDesc}
                    onChange={(_e, d) => setNewDesc(d.value)}
                    placeholder="Specific, measurable, achievable, relevant, time-bound goal description…"
                    rows={4}
                  />
                </Field>
                <Field label="Target Date">
                  <Input
                    type="date"
                    value={newTarget}
                    onChange={(_e, d) => setNewTarget(d.value)}
                  />
                </Field>
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="subtle" onClick={() => setCreateOpen(false)}>Cancel</Button>
              <Button
                appearance="primary"
                onClick={submitCreate}
                disabled={!newTitle.trim() || createMut.isPending}
                icon={createMut.isPending ? <Spinner size="tiny" /> : undefined}
              >
                Save Goal
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* ── Edit dialog ── */}
      <Dialog open={!!editGoal} onOpenChange={(_e, d) => { if (!d.open) setEditGoal(null); }}>
        <DialogSurface>
          <DialogTitle>Edit Goal</DialogTitle>
          <DialogBody>
            <DialogContent>
              <div className={styles.formGrid}>
                <Field label="Title" required>
                  <Input value={editTitle} onChange={(_e, d) => setEditTitle(d.value)} />
                </Field>
                <Field label="Description">
                  <Textarea value={editDesc} onChange={(_e, d) => setEditDesc(d.value)} rows={4} />
                </Field>
                <Field label="Status">
                  <Select
                    value={editStatus}
                    onChange={(_e, d) => setEditStatus(d.value as GoalStatus)}
                  >
                    <option value="Active">Active</option>
                    <option value="Met">Met</option>
                    <option value="NotMet">Not Met</option>
                    <option value="Discontinued">Discontinued</option>
                  </Select>
                </Field>
                <Field label="Target Date">
                  <Input
                    type="date"
                    value={editTarget}
                    onChange={(_e, d) => setEditTarget(d.value)}
                  />
                </Field>
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="subtle" onClick={() => setEditGoal(null)}>Cancel</Button>
              <Button
                appearance="primary"
                onClick={submitEdit}
                disabled={updateMut.isPending}
                icon={updateMut.isPending ? <Spinner size="tiny" /> : undefined}
              >
                Save Changes
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* ── Add progress note dialog ── */}
      <Dialog open={!!progressGoal} onOpenChange={(_e, d) => { if (!d.open) setProgressGoal(null); }}>
        <DialogSurface>
          <DialogTitle>Add Progress Note</DialogTitle>
          <DialogBody>
            <DialogContent>
              {progressGoal && (
                <div className={styles.formGrid}>
                  <Text weight="semibold">{progressGoal.title}</Text>
                  <Field label="Progress Note" required>
                    <Textarea
                      value={progressNote}
                      onChange={(_e, d) => setProgressNote(d.value)}
                      placeholder="Describe progress toward this goal this session…"
                      rows={5}
                    />
                  </Field>
                </div>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="subtle" onClick={() => setProgressGoal(null)}>Cancel</Button>
              <Button
                appearance="primary"
                onClick={submitProgress}
                disabled={!progressNote.trim() || updateMut.isPending}
                icon={updateMut.isPending ? <Spinner size="tiny" /> : undefined}
              >
                Save Note
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* ── Delete confirmation dialog ── */}
      <Dialog open={!!deleteTarget} onOpenChange={(_e, d) => { if (!d.open) setDeleteTarget(null); }}>
        <DialogSurface>
          <DialogTitle>Delete Goal?</DialogTitle>
          <DialogBody>
            <DialogContent>
              <Text>
                Are you sure you want to delete <strong>{deleteTarget?.title}</strong>? This action cannot be undone.
              </Text>
            </DialogContent>
            <DialogActions>
              <Button appearance="subtle" onClick={() => setDeleteTarget(null)}>Cancel</Button>
              <Button
                appearance="primary"
                style={{ backgroundColor: tokens.colorStatusDangerBackground3 }}
                onClick={() => deleteTarget && deleteMut.mutate(deleteTarget.goalId)}
                disabled={deleteMut.isPending}
                icon={deleteMut.isPending ? <Spinner size="tiny" /> : <Delete24Regular />}
              >
                Delete
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* ── AI Suggest dialog ── */}
      <Dialog
        open={suggestOpen && !!suggestQuery.data}
        onOpenChange={(_e, d) => { if (!d.open) { setSuggestOpen(false); setSelectedSuggestions(new Set()); } }}
      >
        <DialogSurface style={{ maxWidth: '560px' }}>
          <DialogTitle>
            <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
              <Sparkle24Regular />
              AI-Suggested Goals
            </div>
          </DialogTitle>
          <DialogBody>
            <DialogContent>
              <div className={styles.formGrid}>
                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                  Select the goals you want to add. You can edit them afterward.
                </Text>
                {(suggestQuery.data ?? []).map((s, i) => (
                  <div key={i} className={styles.suggestCard}>
                    <div className={styles.suggestCardHeader}>
                      <Checkbox
                        label={<Text weight="semibold">{s.title}</Text>}
                        checked={selectedSuggestions.has(i)}
                        onChange={(_e, d) => {
                          setSelectedSuggestions((prev) => {
                            const next = new Set(prev);
                            d.checked ? next.add(i) : next.delete(i);
                            return next;
                          });
                        }}
                      />
                    </div>
                    <Text size={200}>{s.description}</Text>
                  </div>
                ))}
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="subtle" onClick={() => { setSuggestOpen(false); setSelectedSuggestions(new Set()); }}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                icon={<Add24Regular />}
                disabled={selectedSuggestions.size === 0}
                onClick={() => acceptSuggestions(suggestQuery.data ?? [])}
              >
                Add Selected ({selectedSuggestions.size})
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}

// ── GoalCard sub-component ────────────────────────────────────────────────────

interface GoalCardProps {
  goal: GoalResponse;
  styles: ReturnType<typeof useStyles>;
  onEdit: (g: GoalResponse) => void;
  onProgress: (g: GoalResponse) => void;
  onMarkMet: (g: GoalResponse) => void;
  onDelete: (g: GoalResponse) => void;
  isUpdating: boolean;
}

function GoalCard({ goal, styles, onEdit, onProgress, onMarkMet, onDelete, isUpdating }: GoalCardProps) {
  const isActive = goal.status === 'Active';
  return (
    <Card className={styles.goalCard}>
      <div className={styles.goalCardHeader}>
        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, flex: 1, minWidth: 0 }}>
          <Badge appearance="filled" color={STATUS_COLORS[goal.status]}>
            {STATUS_LABELS[goal.status]}
          </Badge>
          <Text className={styles.goalTitle}>{goal.title}</Text>
        </div>
        <div className={styles.goalActions}>
          {isActive && (
            <>
              <Tooltip content="Add progress note" relationship="label">
                <Button
                  size="small"
                  appearance="subtle"
                  icon={<Add24Regular />}
                  onClick={() => onProgress(goal)}
                  disabled={isUpdating}
                />
              </Tooltip>
              <Tooltip content="Mark as Met" relationship="label">
                <Button
                  size="small"
                  appearance="subtle"
                  icon={<Checkmark24Regular />}
                  onClick={() => onMarkMet(goal)}
                  disabled={isUpdating}
                />
              </Tooltip>
            </>
          )}
          <Tooltip content="Edit goal" relationship="label">
            <Button
              size="small"
              appearance="subtle"
              icon={<Edit24Regular />}
              onClick={() => onEdit(goal)}
            />
          </Tooltip>
          <Tooltip content="Delete goal" relationship="label">
            <Button
              size="small"
              appearance="subtle"
              icon={<Delete24Regular />}
              onClick={() => onDelete(goal)}
            />
          </Tooltip>
        </div>
      </div>

      {goal.description && (
        <Text size={200}>{goal.description}</Text>
      )}

      <div className={styles.goalMeta}>
        <span>Created: {fmtDate(goal.createdAt)}</span>
        {goal.targetDate && (
          <span style={isActive && goal.targetDate < new Date().toISOString() ? { color: tokens.colorStatusDangerForeground1 } : {}}>
            Target: {fmtDate(goal.targetDate)}
            {isActive && goal.targetDate < new Date().toISOString() && (
              <Warning24Regular style={{ width: 14, height: 14, marginLeft: 4 }} />
            )}
          </span>
        )}
        {goal.resolvedAt && <span>Resolved: {fmtDate(goal.resolvedAt)}</span>}
        {goal.progressNotes.length > 0 && (
          <span>{goal.progressNotes.length} progress note{goal.progressNotes.length !== 1 ? 's' : ''}</span>
        )}
      </div>

      {goal.progressNotes.length > 0 && (
        <div className={styles.progressNotes}>
          {[...goal.progressNotes].reverse().slice(0, 3).map((n) => (
            <div key={n.noteId} className={styles.progressNoteRow}>
              <Text className={styles.progressNoteDate}>{fmtDate(n.recordedAt)}</Text>
              <Text size={200}>{n.note}</Text>
            </div>
          ))}
          {goal.progressNotes.length > 3 && (
            <Text className={styles.progressNoteDate}>
              +{goal.progressNotes.length - 3} more note{goal.progressNotes.length - 3 !== 1 ? 's' : ''}
            </Text>
          )}
        </div>
      )}
    </Card>
  );
}
