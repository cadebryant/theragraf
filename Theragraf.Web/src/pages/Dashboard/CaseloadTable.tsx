import { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { stripClientIdPrefix } from '@/utils/clientId';
import { getNoteStatus } from '@/utils/noteStatus';
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
  Tooltip,
  Input,
} from '@fluentui/react-components';
import {
  Add24Regular,
  ArrowRight24Regular,
  Warning16Regular,
  Search24Regular,
  ChevronUp16Regular,
  ChevronDown16Regular,
} from '@fluentui/react-icons';
import type { CaseloadSummary, ClientSummary } from '@/types';

const PAGE_SIZE = 10;

type SortKey = 'clientId' | 'lastSessionDate' | 'totalSessions';
type SortDir = 'asc' | 'desc';

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
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalM,
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  toolbar: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
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
  lastSessionCell: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  sortableHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
    cursor: 'pointer',
    userSelect: 'none',
    whiteSpace: 'nowrap',
  },
  paginationRow: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalM,
    paddingTop: tokens.spacingVerticalS,
  },
  pageInfo: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  pageButtons: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
});

interface Props {
  caseload: CaseloadSummary;
}

function parseSessionDate(rowKey: string): Date {
  return new Date(rowKey.replace(/T(\d{2})-(\d{2})-(\d{2})Z/, 'T$1:$2:$3Z'));
}

function compareFn(a: ClientSummary, b: ClientSummary, key: SortKey, dir: SortDir): number {
  let cmp = 0;
  if (key === 'clientId') {
    cmp = stripClientIdPrefix(a.clientId).localeCompare(stripClientIdPrefix(b.clientId));
  } else if (key === 'lastSessionDate') {
    if (!a.lastSessionDate && !b.lastSessionDate) cmp = 0;
    else if (!a.lastSessionDate) cmp = 1;
    else if (!b.lastSessionDate) cmp = -1;
    else cmp = a.lastSessionDate.localeCompare(b.lastSessionDate);
  } else {
    cmp = a.totalSessions - b.totalSessions;
  }
  return dir === 'asc' ? cmp : -cmp;
}

export default function CaseloadTable({ caseload }: Props) {
  const styles = useStyles();
  const navigate = useNavigate();

  const [search, setSearch]     = useState('');
  const [sortKey, setSortKey]   = useState<SortKey>('lastSessionDate');
  const [sortDir, setSortDir]   = useState<SortDir>('desc');
  const [page, setPage]         = useState(1);

  function handleSort(key: SortKey) {
    if (key === sortKey) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('desc');
    }
    setPage(1);
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return caseload.clients.filter((c) =>
      !q || stripClientIdPrefix(c.clientId).toLowerCase().includes(q),
    );
  }, [caseload.clients, search]);

  const sorted = useMemo(
    () => [...filtered].sort((a, b) => compareFn(a, b, sortKey, sortDir)),
    [filtered, sortKey, sortDir],
  );

  const totalPages = Math.max(1, Math.ceil(sorted.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const pageSlice = sorted.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  function SortIcon({ col }: { col: SortKey }) {
    if (sortKey !== col) return <ChevronDown16Regular style={{ opacity: 0.3 }} />;
    return sortDir === 'asc' ? <ChevronUp16Regular /> : <ChevronDown16Regular />;
  }

  return (
    <div className={styles.container}>
      <div className={styles.headerRow}>
        <Text className={styles.title}>
          Caseload ({filtered.length === caseload.clients.length
            ? `${caseload.clients.length} clients`
            : `${filtered.length} of ${caseload.clients.length} clients`})
        </Text>
        <div className={styles.toolbar}>
          <Input
            placeholder="Search client ID…"
            value={search}
            onChange={(_e, d) => { setSearch(d.value); setPage(1); }}
            contentBefore={<Search24Regular />}
            size="small"
            style={{ width: '200px' }}
          />
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => navigate('/sessions/new')}
          >
            New Session
          </Button>
        </div>
      </div>

      {sorted.length === 0 ? (
        <Text className={styles.empty}>
          {search ? 'No clients match your search.' : 'No sessions yet. Start by recording a new session.'}
        </Text>
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHeaderCell onClick={() => handleSort('clientId')}>
                  <div className={styles.sortableHeader}>
                    Client ID <SortIcon col="clientId" />
                  </div>
                </TableHeaderCell>
                <TableHeaderCell onClick={() => handleSort('lastSessionDate')}>
                  <div className={styles.sortableHeader}>
                    Last Session <SortIcon col="lastSessionDate" />
                  </div>
                </TableHeaderCell>
                <TableHeaderCell onClick={() => handleSort('totalSessions')}>
                  <div className={styles.sortableHeader}>
                    Total Sessions <SortIcon col="totalSessions" />
                  </div>
                </TableHeaderCell>
                <TableHeaderCell>Actions</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {pageSlice.map((client) => (
                <TableRow key={client.clientId}>
                  <TableCell>
                    <Text weight="semibold">{stripClientIdPrefix(client.clientId)}</Text>
                  </TableCell>
                  <TableCell>
                    <div className={styles.lastSessionCell}>
                      <span>
                        {client.lastSessionDate
                          ? parseSessionDate(client.lastSessionDate).toLocaleDateString()
                          : '—'}
                      </span>
                      {(() => {
                        const status = getNoteStatus(client.lastSessionDate);
                        if (!status) return null;
                        return (
                          <Tooltip
                            content={status === 'urgent' ? 'Note not documented in 7+ days' : 'Note not documented in 2+ days'}
                            relationship="label"
                          >
                            <Badge
                              appearance="filled"
                              color={status === 'urgent' ? 'danger' : 'warning'}
                              icon={<Warning16Regular />}
                              shape="rounded"
                            >
                              Overdue
                            </Badge>
                          </Tooltip>
                        );
                      })()}
                    </div>
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
                          navigate('/sessions/new', { state: { clientId: client.clientId } })
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

          <div className={styles.paginationRow}>
            <Text className={styles.pageInfo}>
              {sorted.length === 0 ? 'No results' : (
                <>
                  Showing {(currentPage - 1) * PAGE_SIZE + 1}–{Math.min(currentPage * PAGE_SIZE, sorted.length)} of {sorted.length}
                </>
              )}
            </Text>
            <div className={styles.pageButtons}>
              <Button
                size="small"
                appearance="subtle"
                disabled={currentPage === 1}
                onClick={() => setPage(1)}
              >
                «
              </Button>
              <Button
                size="small"
                appearance="subtle"
                disabled={currentPage === 1}
                onClick={() => setPage((p) => p - 1)}
              >
                ‹ Prev
              </Button>
              <Text className={styles.pageInfo}>Page {currentPage} of {totalPages}</Text>
              <Button
                size="small"
                appearance="subtle"
                disabled={currentPage === totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next ›
              </Button>
              <Button
                size="small"
                appearance="subtle"
                disabled={currentPage === totalPages}
                onClick={() => setPage(totalPages)}
              >
                »
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
