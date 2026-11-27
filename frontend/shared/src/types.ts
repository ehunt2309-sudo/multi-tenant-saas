// Mirrors the backend DTOs. Dates are ISO strings in UTC ("...Z").

export type Role = 'PlatformAdmin' | 'Manager' | 'Staff' | 'Customer'
export type BookingStatus = 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled' | 'NoShow'
export type LoyaltyTier = 'Bronze' | 'Silver' | 'Gold' | 'Platinum'
export type DiscountType = 'None' | 'Percentage' | 'FixedAmount'

export interface Paged<T> { items: T[]; page: number; pageSize: number; total: number }

export interface UserInfo { id: string; email: string; firstName: string; lastName: string; role: Role; tenantId: string }
export interface AuthResponse {
  accessToken: string; accessTokenExpiresAt: string
  refreshToken: string; refreshTokenExpiresAt: string
  user: UserInfo
}

export interface PublicTenant {
  name: string; slug: string; industry: string
  primaryColor: string | null; logoUrl: string | null; currency: string; timeZone: string
  loyaltyPointsPerCurrencyUnit: number
}

export interface ServiceOffering { id: string; name: string; description: string | null; durationMinutes: number; price: number; isActive: boolean }
export interface PublicStaff { id: string; name: string; jobTitle: string }

export interface Booking {
  id: string; customerId: string; customerName: string
  staffMemberId: string | null; staffName: string | null
  serviceOfferingId: string; serviceName: string
  startsAt: string; endsAt: string
  status: BookingStatus; partySize: number; price: number; notes: string | null
}
export interface CreateBookingInput {
  serviceOfferingId: string; staffMemberId?: string | null; startsAt: string
  partySize: number; notes?: string | null; customerId?: string | null
}

export interface GuestBookingInput {
  serviceOfferingId: string; startsAt: string; partySize: number; notes?: string | null
  firstName: string; lastName: string; email: string; phone?: string | null
}
export interface GuestBooking { id: string; serviceName: string; startsAt: string; endsAt: string; status: BookingStatus }

export interface Promotion {
  id: string; title: string; description: string | null
  startsAt: string; endsAt: string; discountType: DiscountType; discountValue: number; isActive: boolean
}

export interface LoyaltyAccount {
  customerId: string; pointsBalance: number; lifetimePoints: number
  tier: LoyaltyTier; nextTier: LoyaltyTier | null; pointsToNextTier: number | null
}
export interface LoyaltyTransaction { id: string; points: number; balanceAfter: number; type: 'Earned' | 'Redeemed'; reason: string; createdAt: string }

export interface Customer {
  id: string; firstName: string; lastName: string; email: string | null; phone: string | null
  dateOfBirth: string | null; gender: string | null; marketingConsent: boolean
  totalSpend: number; visitCount: number; lastVisitAt: string | null
}

export interface AnalyticsSummary {
  from: string; to: string; totalBookings: number; bookingsByStatus: Record<string, number>
  revenue: number; newCustomers: number; uniqueCustomersBooked: number
}
export interface TopCustomer { customerId: string; name: string; totalSpend: number; visitCount: number; lastVisitAt: string | null }
export interface DemographicRow { ageBand: string; gender: string; customers: number; totalSpend: number; averageVisits: number }

export interface RegisterInput {
  email: string; password: string; firstName: string; lastName: string
  phone?: string | null; dateOfBirth?: string | null; gender?: string | null; marketingConsent: boolean
}

export interface StaffMember {
  id: string; firstName: string; lastName: string; email: string; role: Role
  jobTitle: string; isActive: boolean; hourlyRate: number | null
}
export interface Shift {
  id: string; staffMemberId: string; staffName: string; startsAt: string; endsAt: string
  clockInAt: string | null; clockOutAt: string | null; notes: string | null
}
export interface HoursRow { staffMemberId: string; staffName: string; scheduledHours: number; workedHours: number; earned: number }
export interface Product { id: string; name: string; description: string | null; price: number; stockQuantity: number; imageUrl: string | null; isActive: boolean }
export interface DailyRevenue { day: string; revenue: number; completedBookings: number }

export interface SiteContent {
  tagline?: string; about?: string; address?: string; phone?: string; email?: string; hours?: string[]
}
export interface PageContent { slug: string; title: string; content: SiteContent & Record<string, unknown>; isPublished: boolean; updatedAt: string | null }

export interface ServiceInput { name: string; description: string | null; durationMinutes: number; price: number; isActive: boolean }
export interface ProductInput { name: string; description: string | null; price: number; stockQuantity: number; imageUrl: string | null; isActive: boolean }
export interface PromotionInput { title: string; description: string | null; startsAt: string; endsAt: string; discountType: DiscountType; discountValue: number; isActive: boolean }
export interface CustomerInput { firstName: string; lastName: string; phone: string | null; dateOfBirth: string | null; gender: string | null; marketingConsent: boolean }
