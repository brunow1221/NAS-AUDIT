import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import AppLayout from '../layouts/AppLayout.vue'
import LoginPage from '../pages/LoginPage.vue'

const DashboardPage = () => import('../pages/DashboardPage.vue')
const HistoryPage = () => import('../pages/HistoryPage.vue')
const ImportsPage = () => import('../pages/ImportsPage.vue')
const LivePage = () => import('../pages/LivePage.vue')
const ExplorerPage = () => import('../pages/ExplorerPage.vue')
const UsersPage = () => import('../pages/UsersPage.vue')
const StatisticsPage = () => import('../pages/StatisticsPage.vue')
const ReportsPage = () => import('../pages/ReportsPage.vue')
const SettingsPage = () => import('../pages/SettingsPage.vue')

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginPage, meta: { public: true, title: 'Acceso seguro' } },
    {
      path: '/',
      component: AppLayout,
      children: [
        { path: '', redirect: '/dashboard' },
        { path: 'dashboard', name: 'dashboard', component: DashboardPage, meta: { title: 'Dashboard' } },
        { path: 'history', name: 'history', component: HistoryPage, meta: { title: 'Historial de auditoría' } },
        { path: 'imports', name: 'imports', component: ImportsPage, meta: { title: 'Importar CSV', administrator: true } },
        { path: 'live', name: 'live', component: LivePage, meta: { title: 'Tiempo real' } },
        { path: 'explorer', name: 'explorer', component: ExplorerPage, meta: { title: 'Explorador histórico' } },
        { path: 'users', name: 'users', component: UsersPage, meta: { title: 'Usuarios SMB' } },
        { path: 'statistics', name: 'statistics', component: StatisticsPage, meta: { title: 'Estadísticas' } },
        { path: 'reports', name: 'reports', component: ReportsPage, meta: { title: 'Informes' } },
        { path: 'settings', name: 'settings', component: SettingsPage, meta: { title: 'Seguridad y operación' } },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/dashboard' },
  ],
})

router.beforeEach(async to => {
  const auth = useAuthStore()
  await auth.initialize()
  if (!to.meta.public && !auth.isAuthenticated) return { name: 'login', query: { redirect: to.fullPath } }
  if (to.name === 'login' && auth.isAuthenticated) return { name: 'dashboard' }
  if (auth.user?.mustChangePassword && to.name !== 'settings') return { name: 'settings' }
  if (to.meta.administrator && !auth.isAdministrator) return { name: 'dashboard' }
  return true
})

export default router

