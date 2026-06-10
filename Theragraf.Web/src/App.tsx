import { BrowserRouter, Route, Routes } from 'react-router-dom';
import AppLayout from '@/components/AppLayout';
import ProtectedRoute from '@/components/ProtectedRoute';
import Dashboard from '@/pages/Dashboard/Dashboard';
import NewSession from '@/pages/NewSession/NewSession';
import SessionReview from '@/pages/SessionReview/SessionReview';
import ClientProfile from '@/pages/ClientProfile/ClientProfile';
import SessionDetail from '@/pages/SessionDetail/SessionDetail';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppLayout />}>
          <Route
            index
            element={
              <ProtectedRoute>
                <Dashboard />
              </ProtectedRoute>
            }
          />
          <Route
            path="sessions/new"
            element={
              <ProtectedRoute>
                <NewSession />
              </ProtectedRoute>
            }
          />
          <Route
            path="sessions/review"
            element={
              <ProtectedRoute>
                <SessionReview />
              </ProtectedRoute>
            }
          />
          <Route
            path="sessions/:clientId"
            element={
              <ProtectedRoute>
                <ClientProfile />
              </ProtectedRoute>
            }
          />
          <Route
            path="sessions/:clientId/:sessionDate"
            element={
              <ProtectedRoute>
                <SessionDetail />
              </ProtectedRoute>
            }
          />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
