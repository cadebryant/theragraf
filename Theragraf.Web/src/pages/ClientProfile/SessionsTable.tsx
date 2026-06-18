import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  makeStyles,
  tokens,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Button,
  Badge,
  Select,
  Text,
  Spinner,
  MessageBar,
  MessageBarBody,
} from '@fluentui/react-components';
import {
  ArrowRight24Regular,
  Delete24Regular,
  ChevronLeft24Regular,
  ChevronRight24Regular,
} from '@fluentui/react-icons';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getSessionsByClient, deleteSession } from '@/api/sessions';
import { formatSessionDate } from '@/utils/dateFormat';
import { formatErrorMessage } from '@/utils/errorMessages';
import type { SessionResponse } from '@/types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  toolbar: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
  },
  pagination: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginLeft: 'auto',
  },
});

interface Props {
  clientId: string;
}

export default function SessionsTable({ clientId }: Props) {
  const styles = useStyles();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [discipline, setDiscipline] = useState('');
  const [payer, setPayer] = useState('');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [continuationToken, setContinuationToken] = useState<string | null>(null);
  const [tokenStack, setTokenStack] = useState<string[]>([]);

  const { data, isLoading, error } = useQuery({
    queryKey: ['sessions', clientId, discipline, payer, sortOrder, continuationToken],
    queryFn: () =>
      getSessionsByClient(clientId, {
        pageSize: 10,
        discipline: discipline || undefined,
        payer: payer || undefined,
        sortOrder,
        continuationToken: continuationToken ?? undefined,
      }),
  });

  const deleteMutation = useMutation({
    mutationFn: ({ date }: { date: string }) => deleteSession(clientId, date),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['sessions', clientId] });
    },
  });

  function nextPage() {
    if (data?.continuationToken) {
      setTokenStack((s) => [...s, continuationToken ?? '']);
      setContinuationToken(data.continuationToken);
    }
  }

  function prevPage() {
    const stack = [...tokenStack];
    const prev = stack.pop() ?? null;
    setTokenStack(stack);
    setContinuationToken(prev);
  }

  if (isLoading) return <Spinner label="Loading sessions…" />;
  if (error) return <Text style={{ color: tokens.colorStatusDangerForeground1 }}>{formatErrorMessage(error, 'loading sessions')}</Text>;

  const sessions: SessionResponse[] = data?.items ?? [];

  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <Select
          value={discipline}
          onChange={(_e, d) => { setDiscipline(d.value); setContinuationToken(null); setTokenStack([]); }}
          size="small"
        >
          <option value="">All Disciplines</option>
          <option value="OccupationalTherapy">OT</option>
          <option value="PhysicalTherapy">PT</option>
          <option value="Psychotherapy">Psychotherapy</option>
        </Select>

        <Select
          value={payer}
          onChange={(_e, d) => { setPayer(d.value); setContinuationToken(null); setTokenStack([]); }}
          size="small"
        >
          <option value="">All Payers</option>
          <option value="Medicare">Medicare</option>
          <option value="MedicareAdvantage">Medicare Advantage</option>
          <option value="Medicaid">Medicaid</option>
          <option value="Commercial">Commercial</option>
          <option value="SelfPay">Self-Pay</option>
        </Select>

        <Select
          value={sortOrder}
          onChange={(_e, d) => setSortOrder(d.value as 'asc' | 'desc')}
          size="small"
        >
          <option value="desc">Newest First</option>
          <option value="asc">Oldest First</option>
        </Select>

        <div className={styles.pagination}>
          <Button
            size="small"
            icon={<ChevronLeft24Regular />}
            disabled={tokenStack.length === 0}
            onClick={prevPage}
          />
          <Button
            size="small"
            icon={<ChevronRight24Regular />}
            disabled={!data?.hasMore}
            onClick={nextPage}
          />
        </div>
      </div>

      {deleteMutation.error && (
        <MessageBar intent="error">
          <MessageBarBody>{formatErrorMessage(deleteMutation.error, 'deleting session')}</MessageBarBody>
        </MessageBar>
      )}

      {sessions.length === 0 ? (
        <Text style={{ color: tokens.colorNeutralForeground3 }}>No sessions found.</Text>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Date</TableHeaderCell>
              <TableHeaderCell>Discipline</TableHeaderCell>
              <TableHeaderCell>Setting</TableHeaderCell>
              <TableHeaderCell>Payer</TableHeaderCell>
              <TableHeaderCell>Duration</TableHeaderCell>
              <TableHeaderCell>Billable Units</TableHeaderCell>
              <TableHeaderCell>Actions</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sessions.map((s) => (
              <TableRow key={s.sessionDate}>
                <TableCell>{formatSessionDate(s.sessionDate)}</TableCell>
                <TableCell>
                  <Badge appearance="tint" color="brand">{s.discipline}</Badge>
                </TableCell>
                <TableCell>{s.setting}</TableCell>
                <TableCell>{s.payer}</TableCell>
                <TableCell>
                  {s.sessionDurationMinutes != null ? `${s.sessionDurationMinutes} min` : '—'}
                </TableCell>
                <TableCell>
                  {s.suggestedCptCodes.reduce((sum, c) => sum + c.billableUnits, 0)}
                </TableCell>
                <TableCell>
                  <Button
                    size="small"
                    icon={<ArrowRight24Regular />}
                    onClick={() =>
                      navigate(
                        `/sessions/${encodeURIComponent(clientId)}/${encodeURIComponent(s.sessionDate)}`,
                      )
                    }
                  >
                    View
                  </Button>
                  <Button
                    size="small"
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    style={{ marginLeft: 4 }}
                    onClick={() => {
                      if (confirm('Delete this session?')) {
                        deleteMutation.mutate({ date: s.sessionDate });
                      }
                    }}
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
