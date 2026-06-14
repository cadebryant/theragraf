import { useNavigate } from 'react-router-dom';
import { stripClientIdPrefix } from '@/utils/clientId';
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
  Text,
} from '@fluentui/react-components';
import { Add24Regular, ArrowRight24Regular } from '@fluentui/react-icons';
import type { CaseloadSummary } from '@/types';

const useStyles = makeStyles({
  container: {
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
  empty: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
  actionsCell: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
  },
});

interface Props {
  caseload: CaseloadSummary;
}

export default function CaseloadTable({ caseload }: Props) {
  const styles = useStyles();
  const navigate = useNavigate();

  const sorted = [...caseload.clients].sort((a, b) => {
    if (!a.lastSessionDate) return 1;
    if (!b.lastSessionDate) return -1;
    return b.lastSessionDate.localeCompare(a.lastSessionDate);
  });

  return (
    <div className={styles.container}>
      <div className={styles.headerRow}>
        <Text className={styles.title}>Caseload ({caseload.clients.length} clients)</Text>
        <Button
          appearance="primary"
          icon={<Add24Regular />}
          onClick={() => navigate('/sessions/new')}
        >
          New Session
        </Button>
      </div>

      {sorted.length === 0 ? (
        <Text className={styles.empty}>No sessions yet. Start by recording a new session.</Text>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Client ID</TableHeaderCell>
              <TableHeaderCell>Last Session</TableHeaderCell>
              <TableHeaderCell>Total Sessions</TableHeaderCell>
              <TableHeaderCell>Actions</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sorted.map((client) => (
              <TableRow key={client.clientId}>
                <TableCell>
                  <Text weight="semibold">{stripClientIdPrefix(client.clientId)}</Text>
                </TableCell>
                <TableCell>
                  {client.lastSessionDate
                    ? new Date(
                        client.lastSessionDate.replace(
                          /T(\d{2})-(\d{2})-(\d{2})Z/,
                          'T$1:$2:$3Z',
                        ),
                      ).toLocaleDateString()
                    : '—'}
                </TableCell>
                <TableCell>
                  <Badge appearance="filled" color="brand">
                    {client.totalSessions}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div className={styles.actionsCell}>
                    <Button
                      size="small"
                      icon={<ArrowRight24Regular />}
                      onClick={() => navigate(`/sessions/${encodeURIComponent(client.clientId)}`)}
                    >
                      View
                    </Button>
                    <Button
                      size="small"
                      appearance="primary"
                      icon={<Add24Regular />}
                      onClick={() =>
                        navigate('/sessions/new', {
                          state: { clientId: client.clientId },
                        })
                      }
                    >
                      New Session
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
