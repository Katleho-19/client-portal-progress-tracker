-- ============================================================================
-- Client Portal & Progress Tracker - Supabase PostgreSQL Schema & Security
-- Author: Katleho Innocent Makhoali
-- ============================================================================

-- 1. FREELANCERS TABLE
CREATE TABLE public.freelancers (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    full_name VARCHAR(255) NOT NULL,
    business_name VARCHAR(255) NOT NULL DEFAULT '',
    logo_url VARCHAR(500) NULL,
    phone_number VARCHAR(50) NOT NULL DEFAULT '',
    email VARCHAR(255) NOT NULL,
    bank_name VARCHAR(100) NULL,
    account_number VARCHAR(50) NULL,
    branch_code VARCHAR(20) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 2. PROJECTS TABLE
CREATE TABLE public.projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    freelancer_id UUID NOT NULL REFERENCES public.freelancers(id) ON DELETE CASCADE,
    reference_code VARCHAR(50) NOT NULL UNIQUE,
    portal_slug VARCHAR(150) NOT NULL UNIQUE,
    is_link_active BOOLEAN NOT NULL DEFAULT TRUE,
    quote_expires_at TIMESTAMPTZ NULL,
    client_name VARCHAR(255) NOT NULL,
    client_email VARCHAR(255) NOT NULL,
    title VARCHAR(255) NOT NULL,
    total_amount DECIMAL(10, 2) NOT NULL CHECK (total_amount >= 0),
    deposit_percentage INT NOT NULL CHECK (deposit_percentage >= 0 AND deposit_percentage <= 100),
    amount_paid DECIMAL(10, 2) NOT NULL DEFAULT 0.00 CHECK (amount_paid >= 0),
    status VARCHAR(50) NOT NULL DEFAULT 'Draft'
        CHECK (status IN ('Draft', 'Awaiting_Deposit', 'Active', 'Completed_Unpaid', 'Paid_In_Full')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 3. QUOTE SIGNATURES TABLE (1-to-1 with projects, immutable ECTA audit log)
CREATE TABLE public.quote_signatures (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL UNIQUE REFERENCES public.projects(id) ON DELETE CASCADE,
    signer_full_name VARCHAR(255) NOT NULL,
    agreed_to_terms BOOLEAN NOT NULL CHECK (agreed_to_terms = TRUE),
    ip_address VARCHAR(100) NOT NULL,
    user_agent TEXT NOT NULL,
    signed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 4. MILESTONES TABLE
CREATE TABLE public.milestones (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL REFERENCES public.projects(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    sort_order INT NOT NULL DEFAULT 1,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending'
        CHECK (status IN ('Pending', 'In_Progress', 'Completed')),
    completed_at TIMESTAMPTZ NULL
);

-- 5. TRANSACTIONS TABLE
CREATE TABLE public.transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL REFERENCES public.projects(id) ON DELETE CASCADE,
    payment_method VARCHAR(50) NOT NULL
        CHECK (payment_method IN ('Paystack', 'Manual_EFT')),
    external_reference VARCHAR(150) NULL UNIQUE,
    pop_file_path VARCHAR(500) NULL,
    amount DECIMAL(10, 2) NOT NULL CHECK (amount > 0),
    payment_type VARCHAR(50) NOT NULL
        CHECK (payment_type IN ('Deposit', 'Final_Balance', 'Full_Payment')),
    status VARCHAR(50) NOT NULL DEFAULT 'Pending_Verification'
        CHECK (status IN ('Pending_Verification', 'Success', 'Failed')),
    paid_at TIMESTAMPTZ NULL
);

-- 6. PERFORMANCE INDEXES
CREATE INDEX idx_projects_portal_slug ON public.projects(portal_slug);
CREATE INDEX idx_projects_freelancer_id ON public.projects(freelancer_id);
CREATE INDEX idx_milestones_project_id ON public.milestones(project_id);
CREATE INDEX idx_transactions_project_id ON public.transactions(project_id);

-- 7. AUTO-CREATE FREELANCER PROFILE ON SIGNUP (FR-01)
CREATE OR REPLACE FUNCTION public.handle_new_freelancer()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.freelancers (id, full_name, email)
    VALUES (
        NEW.id,
        COALESCE(NEW.raw_user_meta_data->>'full_name', 'Freelancer'),
        NEW.email
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_freelancer();

-- 8. ENABLE ROW-LEVEL SECURITY ON ALL 5 TABLES (NFR-02)
ALTER TABLE public.freelancers ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.projects ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.quote_signatures ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.milestones ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.transactions ENABLE ROW LEVEL SECURITY;

-- 9. RLS POLICIES: FREELANCERS ONLY ACCESS THEIR OWN DATA
CREATE POLICY "Freelancers manage own profile"
    ON public.freelancers FOR ALL
    USING (auth.uid() = id)
    WITH CHECK (auth.uid() = id);

CREATE POLICY "Freelancers manage own projects"
    ON public.projects FOR ALL
    USING (auth.uid() = freelancer_id)
    WITH CHECK (auth.uid() = freelancer_id);

CREATE POLICY "Freelancers manage own milestones"
    ON public.milestones FOR ALL
    USING (
        EXISTS (
            SELECT 1 FROM public.projects
            WHERE projects.id = milestones.project_id
              AND projects.freelancer_id = auth.uid()
        )
    );

-- 10. IMMUTABLE ECTA AUDIT TRAIL (NFR-03: SELECT ONLY — NO UPDATE/DELETE)
CREATE POLICY "Freelancers view own quote signatures"
    ON public.quote_signatures FOR SELECT
    USING (
        EXISTS (
            SELECT 1 FROM public.projects
            WHERE projects.id = quote_signatures.project_id
              AND projects.freelancer_id = auth.uid()
        )
    );

-- 11. TRANSACTIONS POLICIES
CREATE POLICY "Freelancers view and verify own transactions"
    ON public.transactions FOR SELECT
    USING (
        EXISTS (
            SELECT 1 FROM public.projects
            WHERE projects.id = transactions.project_id
              AND projects.freelancer_id = auth.uid()
        )
    );

CREATE POLICY "Freelancers update manual EFT status"
    ON public.transactions FOR UPDATE
    USING (
        EXISTS (
            SELECT 1 FROM public.projects
            WHERE projects.id = transactions.project_id
              AND projects.freelancer_id = auth.uid()
        )
    );

-- 12. PRIVATE STORAGE BUCKET FOR PROOF OF PAYMENT (POP) PDFs (FR-14)
INSERT INTO storage.buckets (id, name, public)
VALUES ('proof-of-payments', 'proof-of-payments', false)
ON CONFLICT (id) DO NOTHING;

CREATE POLICY "Authenticated freelancers view POPs"
    ON storage.objects FOR SELECT
    USING (bucket_id = 'proof-of-payments' AND auth.role() = 'authenticated');
