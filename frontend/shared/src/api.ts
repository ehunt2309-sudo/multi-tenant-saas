import { request } from './http'
import type * as T from './types'

const page = (p = 1, pageSize = 25) => ({ page: p, pageSize })
const json = <B,>(method: 'POST' | 'PUT' | 'PATCH', body: B) => ({ method, body })

/** One function per backend endpoint, so UI code never builds URLs by hand. */
export const api = {
  auth: {
    login: (email: string, password: string) =>
      request<T.AuthResponse>('/api/auth/login', { method: 'POST', body: { email, password }, auth: false }),
    register: (input: T.RegisterInput) =>
      request<T.AuthResponse>('/api/auth/register', { method: 'POST', body: input, auth: false }),
    logout: (refreshToken: string) =>
      request<void>('/api/auth/logout', { method: 'POST', body: { refreshToken }, auth: false }),
    me: () => request<T.UserInfo>('/api/auth/me'),
    changePassword: (currentPassword: string, newPassword: string) =>
      request<void>('/api/auth/change-password', { method: 'POST', body: { currentPassword, newPassword } }),
  },
  tenant: {
    public: () => request<T.PublicTenant>('/api/tenant/public', { auth: false }),
    updateSettings: (b: { primaryColor: string | null; logoUrl: string | null; loyaltyPointsPerCurrencyUnit: number }) =>
      request<T.PublicTenant>('/api/tenant/settings', json('PUT', b)),
  },
  pages: {
    getPublic: (slug: string) => request<T.PageContent>(`/api/pages/${slug}`, { auth: false }),
    getForEdit: (slug: string) => request<T.PageContent>(`/api/pages/${slug}/edit`),
    upsert: (slug: string, b: { title: string; content: T.SiteContent; isPublished: boolean }) =>
      request<T.PageContent>(`/api/pages/${slug}`, json('PUT', b)),
  },
  services: {
    list: () => request<T.ServiceOffering[]>('/api/services', { auth: false }),
    all: () => request<T.ServiceOffering[]>('/api/services/all'),
    create: (b: T.ServiceInput) => request<T.ServiceOffering>('/api/services', json('POST', b)),
    update: (id: string, b: T.ServiceInput) => request<T.ServiceOffering>(`/api/services/${id}`, json('PUT', b)),
  },
  products: {
    list: (p = 1) => request<T.Paged<T.Product>>('/api/products', { auth: false, query: page(p, 50) }),
    all: (p = 1) => request<T.Paged<T.Product>>('/api/products/all', { query: page(p, 100) }),
    create: (b: T.ProductInput) => request<T.Product>('/api/products', json('POST', b)),
    update: (id: string, b: T.ProductInput) => request<T.Product>(`/api/products/${id}`, json('PUT', b)),
    adjustStock: (id: string, delta: number) => request<T.Product>(`/api/products/${id}/stock`, json('PATCH', { delta })),
  },
  staff: {
    public: () => request<T.PublicStaff[]>('/api/staff/public', { auth: false }),
    list: () => request<T.StaffMember[]>('/api/staff'),
    create: (b: { email: string; password: string; firstName: string; lastName: string; jobTitle: string; hourlyRate: number; role: 'Staff' | 'Manager' }) =>
      request<T.StaffMember>('/api/staff', json('POST', b)),
    update: (id: string, b: { firstName: string; lastName: string; jobTitle: string; hourlyRate: number; isActive: boolean }) =>
      request<T.StaffMember>(`/api/staff/${id}`, json('PUT', b)),
  },
  shifts: {
    list: (from: string, to: string, staffId?: string) => request<T.Shift[]>('/api/shifts', { query: { from, to, staffId } }),
    mine: (from: string, to: string) => request<T.Shift[]>('/api/shifts/mine', { query: { from, to } }),
    create: (b: { staffMemberId: string; startsAt: string; endsAt: string; notes: string | null }) =>
      request<T.Shift>('/api/shifts', json('POST', b)),
    remove: (id: string) => request<void>(`/api/shifts/${id}`, { method: 'DELETE' }),
    clockIn: (id: string) => request<T.Shift>(`/api/shifts/${id}/clock-in`, { method: 'POST' }),
    clockOut: (id: string) => request<T.Shift>(`/api/shifts/${id}/clock-out`, { method: 'POST' }),
    hours: (from: string, to: string) => request<T.HoursRow[]>('/api/shifts/hours', { query: { from, to } }),
    myHours: (from: string, to: string) => request<T.HoursRow[]>('/api/shifts/hours/mine', { query: { from, to } }),
  },
  promotions: {
    active: () => request<T.Promotion[]>('/api/promotions/active', { auth: false }),
    list: (p = 1) => request<T.Paged<T.Promotion>>('/api/promotions', { query: page(p, 100) }),
    create: (b: T.PromotionInput) => request<T.Promotion>('/api/promotions', json('POST', b)),
    update: (id: string, b: T.PromotionInput) => request<T.Promotion>(`/api/promotions/${id}`, json('PUT', b)),
    remove: (id: string) => request<void>(`/api/promotions/${id}`, { method: 'DELETE' }),
  },
  bookings: {
    create: (input: T.CreateBookingInput) => request<T.Booking>('/api/bookings', { method: 'POST', body: input }),
    createGuest: (input: T.GuestBookingInput) =>
      request<T.GuestBooking>('/api/bookings/guest', { method: 'POST', body: input, auth: false }),
    mine: (p = 1) => request<T.Paged<T.Booking>>('/api/bookings/mine', { query: page(p, 50) }),
    list: (q: { from?: string; to?: string; status?: T.BookingStatus; page?: number }) =>
      request<T.Paged<T.Booking>>('/api/bookings', { query: { from: q.from, to: q.to, status: q.status, ...page(q.page, 25) } }),
    cancel: (id: string) => request<T.Booking>(`/api/bookings/${id}/cancel`, { method: 'POST' }),
    setStatus: (id: string, status: T.BookingStatus) => request<T.Booking>(`/api/bookings/${id}/status`, json('PATCH', { status })),
  },
  loyalty: {
    me: () => request<T.LoyaltyAccount>('/api/loyalty/me'),
    myHistory: (p = 1) => request<T.Paged<T.LoyaltyTransaction>>('/api/loyalty/me/history', { query: page(p, 50) }),
    account: (customerId: string) => request<T.LoyaltyAccount>(`/api/loyalty/customers/${customerId}`),
    history: (customerId: string) => request<T.Paged<T.LoyaltyTransaction>>(`/api/loyalty/customers/${customerId}/history`, { query: page(1, 20) }),
    award: (customerId: string, points: number, reason: string) =>
      request<T.LoyaltyAccount>(`/api/loyalty/customers/${customerId}/award`, json('POST', { points, reason })),
    redeem: (customerId: string, points: number, reason: string) =>
      request<T.LoyaltyAccount>(`/api/loyalty/customers/${customerId}/redeem`, json('POST', { points, reason })),
  },
  customers: {
    me: () => request<T.Customer>('/api/customers/me'),
    updateMe: (b: T.CustomerInput) => request<T.Customer>('/api/customers/me', json('PUT', b)),
    list: (search?: string, p = 1, size = 25) => request<T.Paged<T.Customer>>('/api/customers', { query: { search, ...page(p, size) } }),
    create: (b: T.CustomerInput & { email: string | null }) => request<T.Customer>('/api/customers', json('POST', b)),
    update: (id: string, b: T.CustomerInput) => request<T.Customer>(`/api/customers/${id}`, json('PUT', b)),
    erase: (id: string) => request<void>(`/api/customers/${id}`, { method: 'DELETE' }),
  },
  analytics: {
    summary: (from?: string, to?: string) => request<T.AnalyticsSummary>('/api/analytics/summary', { query: { from, to } }),
    revenueByDay: (from?: string, to?: string) => request<T.DailyRevenue[]>('/api/analytics/revenue-by-day', { query: { from, to } }),
    topCustomers: (take = 10) => request<T.TopCustomer[]>('/api/analytics/top-customers', { query: { take } }),
    demographics: () => request<T.DemographicRow[]>('/api/analytics/demographics'),
  },
}
