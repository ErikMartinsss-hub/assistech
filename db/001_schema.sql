-- =====================================================================
-- Assistech - Schema base (PostgreSQL / Supabase)
-- Execute no SQL Editor do Supabase ou via psql.
-- =====================================================================

create extension if not exists "pgcrypto";

-- ---------------------------------------------------------------------
-- ENUMs
-- ---------------------------------------------------------------------
do $$ begin
  create type tipo_equipamento as enum ('celular', 'desktop', 'notebook', 'tablet', 'outro');
exception when duplicate_object then null; end $$;

do $$ begin
  create type status_os as enum (
    'aberta', 'em_analise', 'aguardando_pecas', 'aguardando_cliente',
    'aguardando_aprovacao', 'em_reparo', 'pronta_entrega', 'entregue',
    'cancelada'
  );
exception when duplicate_object then null; end $$;

do $$ begin
  create type tipo_item_os as enum ('peca', 'servico', 'deslocamento');
exception when duplicate_object then null; end $$;

do $$ begin
  create type perfil_usuario as enum ('admin', 'tecnico', 'atendimento');
exception when duplicate_object then null; end $$;

-- ---------------------------------------------------------------------
-- EMPRESAS (tenant) - suporta multi-loja no mesmo banco
-- ---------------------------------------------------------------------
create table if not exists public.empresas (
  id                uuid primary key default gen_random_uuid(),
  nome              text        not null,
  documento         text,
  telefone          text,
  whatsapp          text,
  email             text,
  endereco          text,
  logo              bytea,
  logo_content_type text,
  cor_primaria      text        not null default '#0d6efd',
  criado_em         timestamptz not null default now()
);

comment on table  public.empresas is 'Configuracao da loja / marca personalizada';
comment on column public.empresas.logo is 'Logo em bytes (PNG/JPG), servida em GET /api/config/logo/{empresaId}';

-- ---------------------------------------------------------------------
-- VINCULO USUARIO <-> EMPRESA
-- auth.users.id (id do Supabase Auth) = usuario_id
-- ---------------------------------------------------------------------
create table if not exists public.usuarios (
  id            uuid primary key default gen_random_uuid(),
  empresa_id    uuid           not null references public.empresas (id) on delete cascade,
  usuario_id    uuid           not null unique,
  nome          text           not null,
  email         text,
  perfil        perfil_usuario not null default 'tecnico',
  ativo         boolean        not null default true,
  criado_em     timestamptz    not null default now()
);

create index if not exists ix_usuarios_empresa on public.usuarios (empresa_id);

-- ---------------------------------------------------------------------
-- CLIENTES
-- ---------------------------------------------------------------------
create table if not exists public.clientes (
  id            uuid primary key default gen_random_uuid(),
  empresa_id    uuid           not null references public.empresas (id) on delete cascade,
  nome          text           not null,
  documento     text,
  telefone      text           not null,
  email         text,
  endereco      text,
  observacoes   text,
  criado_em     timestamptz    not null default now(),
  atualizado_em timestamptz    not null default now()
);

create index if not exists ix_clientes_empresa_nome  on public.clientes (empresa_id, lower(nome));
create index if not exists ix_clientes_empresa_fone  on public.clientes (empresa_id, telefone);

-- ---------------------------------------------------------------------
-- EQUIPAMENTOS
-- ---------------------------------------------------------------------
create table if not exists public.equipamentos (
  id            uuid primary key default gen_random_uuid(),
  empresa_id    uuid           not null references public.empresas (id) on delete cascade,
  cliente_id    uuid           not null references public.clientes (id) on delete cascade,
  tipo          tipo_equipamento not null default 'celular',
  marca         text           not null,
  modelo        text           not null,
  numero_serie  text,
  cor           text,
  ano           int,
  observacoes   text,
  criado_em     timestamptz    not null default now()
);

create index if not exists ix_equipamentos_empresa   on public.equipamentos (empresa_id);
create index if not exists ix_equipamentos_cliente   on public.equipamentos (cliente_id);

-- ---------------------------------------------------------------------
-- ORDENS DE SERVICO
-- ---------------------------------------------------------------------
create table if not exists public.ordens_servico (
  id                   uuid primary key default gen_random_uuid(),
  empresa_id           uuid        not null references public.empresas (id) on delete cascade,
  numero               int         not null,
  cliente_id           uuid        not null references public.clientes (id) on delete restrict,
  equipamento_id       uuid        not null references public.equipamentos (id) on delete restrict,
  status               status_os   not null default 'aberta',
  tipo_servico         text,
  relato_cliente       text,
  diagnostico          text,
  laudo_conclusao      text,
  senha_equipamento    text,
  acessorios           text,
  orcamento_valor      numeric(14, 2),
  custo_pecas          numeric(14, 2) not null default 0,
  desconto             numeric(14, 2) not null default 0,
  garantia_dias        int         not null default 90,
  data_entrada         timestamptz not null default now(),
  data_previsao        timestamptz,
  data_conclusao       timestamptz,
  criado_por           text,
  criado_em            timestamptz not null default now(),
  atualizado_em        timestamptz not null default now(),
  constraint uk_os_empresa_numero unique (empresa_id, numero)
);

create index if not exists ix_os_empresa_status  on public.ordens_servico (empresa_id, status);
create index if not exists ix_os_empresa_cliente on public.ordens_servico (empresa_id, cliente_id);
create index if not exists ix_os_empresa_entrada on public.ordens_servico (empresa_id, data_entrada desc);

-- ---------------------------------------------------------------------
-- ITENS DA OS (pecas, servicos, deslocamento)
-- ---------------------------------------------------------------------
create table if not exists public.os_itens (
  id             uuid primary key default gen_random_uuid(),
  empresa_id     uuid           not null references public.empresas (id) on delete cascade,
  ordem_servico_id uuid         not null references public.ordens_servico (id) on delete cascade,
  tipo           tipo_item_os   not null default 'servico',
  descricao      text           not null,
  quantidade     numeric(10, 2) not null default 1,
  valor_unitario numeric(14, 2) not null default 0,
  custo_unitario numeric(14, 2) not null default 0,
  criado_em      timestamptz    not null default now()
);

create index if not exists ix_os_itens_os on public.os_itens (ordem_servico_id);

-- ---------------------------------------------------------------------
-- HISTORICO / TRILHA DE AUDITORIA DA OS
-- ---------------------------------------------------------------------
create table if not exists public.os_historico (
  id              uuid primary key default gen_random_uuid(),
  empresa_id      uuid        not null references public.empresas (id) on delete cascade,
  ordem_servico_id uuid       not null references public.ordens_servico (id) on delete cascade,
  status_anterior status_os,
  status_novo     status_os   not null,
  comentario      text,
  usuario_nome    text,
  criado_em       timestamptz not null default now()
);

create index if not exists ix_os_historico_os on public.os_historico (ordem_servico_id, criado_em);

-- ---------------------------------------------------------------------
-- NUMERACAO SEQUENCIAL DE OS POR EMPRESA
-- ---------------------------------------------------------------------
create or replace function public.proxima_os(p_empresa uuid)
returns int
language plpgsql
as $$
declare
  v_numero int;
begin
  select coalesce(max(numero), 0) + 1
    into v_numero
    from public.ordens_servico
   where empresa_id = p_empresa;

  return v_numero;
end;
$$;

-- ---------------------------------------------------------------------
-- updated_at automatico
-- ---------------------------------------------------------------------
create or replace function public.touch_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.atualizado_em := now();
  return new;
end;
$$;

drop trigger if exists tg_clientes_touch on public.clientes;
create trigger tg_clientes_touch
  before update on public.clientes
  for each row execute function public.touch_updated_at();

drop trigger if exists tg_os_touch on public.ordens_servico;
create trigger tg_os_touch
  before update on public.ordens_servico
  for each row execute function public.touch_updated_at();
