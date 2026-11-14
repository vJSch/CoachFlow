import { createBrowserRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { AuthProvider } from './AuthProvider'
import { ExerciseEditPage } from './exercises/ExerciseEditPage'
import { ExercisesPage } from './exercises/ExercisesPage'
import { Layout, RequireAccount, RequireAnonymous } from './Layout'
import { AcceptInvitationPage } from './pages/AcceptInvitationPage'
import { ForgotPasswordPage } from './pages/ForgotPasswordPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { PlanEditPage } from './plans/PlanEditPage'
import { PlansPage } from './plans/PlansPage'
import { PlanViewPage } from './plans/PlanViewPage'
import { ResetPasswordPage } from './pages/ResetPasswordPage'

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      {
        element: <RequireAccount />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'uebungen', element: <ExercisesPage /> },
          { path: 'uebungen/neu', element: <ExerciseEditPage /> },
          { path: 'uebungen/:id', element: <ExerciseEditPage key="edit" /> },
          { path: 'plaene', element: <PlansPage /> },
          { path: 'plaene/neu', element: <PlanEditPage /> },
          { path: 'plaene/:id', element: <PlanViewPage /> },
          { path: 'plaene/:id/bearbeiten', element: <PlanEditPage key="edit" /> },
        ],
      },
      {
        element: <RequireAnonymous />,
        children: [
          { path: 'anmelden', element: <LoginPage /> },
          { path: 'passwort-vergessen', element: <ForgotPasswordPage /> },
          { path: 'passwort-neu', element: <ResetPasswordPage /> },
          { path: 'einladung', element: <AcceptInvitationPage /> },
        ],
      },
      { path: '*', element: <p>Diese Seite gibt es nicht.</p> },
    ],
  },
])

function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}

export default App
