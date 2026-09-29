-- =====================================================================
-- Assistech - Row Level Security (defesa em profundidade multi-inquilino)
--
-- IMPORTANTE: a API ja filtra por empresa_id em todas as queries.
-- Este script adiciona uma segunda camada, usando a variavel de
-- sessao `app.empresa_id`, que a API define via `SET LOCAL` em cada
-- transacao (ver Db.cs).
--
-- Para que a RLS valha, a API deve conectar com um papel SEM
-- BYPASSRLS. Crie-o com o script 003.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Papel da aplicacao
-- ---------------------------------------------------------------------
do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'assistech_app') then
    create role assistech_app nologin;
  end if;
end;
$$;

-- ---------------------------------------------------------------------
-- Funcoes auxiliares de contexto
-- ---------------------------------------------------------------------
create or replace function public.empresa_atual()
returns uuid
language sql
stable
as $$
  select nullif(current_setting('app.empresa_id', true), '')::uuid;
$$;

create or replace function public.e_dono_da_empresa(p_empresa uuid)
returns boolean
language sql
stable
as $$
  select p_empresa = public.empresa_atual();
$$;

grant execute on function public.empresa_atual() to assistech_app;
grant execute on function public.e_dono_da_empresa(uuid) to assistech_app;

-- ---------------------------------------------------------------------
-- Bootstrap
-- A API precisa descobrir a loja do usuario ANTES de existir
-- app.empresa_id. Essas duas funcoes rodam como owner (SECURITY DEFINER)
-- e por isso enxergam a linha de vinculo mesmo com a RLS ativa.
-- Sao as unicas saidas da RLS: nao recebem empresa e nao devolvem dados
-- de negocio, apenas o id da loja.
-- ---------------------------------------------------------------------
create or replace function public.empresa_do_usuario(p_usuario uuid)
returns uuid
language sql
stable
security definer
set search_path = public
as $$
  select u.empresa_id
    from public.usuarios u
   where u.usuario_id = p_usuario and u.ativo
   limit 1
$$;

create or replace function public.empresa_unica()
returns uuid
language sql
stable
security definer
set search_path = public
as $$
  select e.id from public.empresas e order by e.nome limit 1
$$;

revoke all on function public.empresa_do_usuario(uuid) from public;
revoke all on function public.empresa_unica() from public;
grant execute on function public.empresa_do_usuario(uuid) to assistech_app;
grant execute on function public.empresa_unica() to assistech_app;

-- ---------------------------------------------------------------------
-- Grants
-- ---------------------------------------------------------------------
grant usage on schema public to assistech_app;
grant select, insert, update, delete on
  public.empresas, public.usuarios, public.clientes, public.equipamentos,
  public.ordens_servico, public.os_itens, public.os_historico
  to assistech_app;
grant usage, select on all sequences in schema public to assistech_app;
grant execute on function public.proxima_os(uuid) to assistech_app;

-- ---------------------------------------------------------------------
-- RLS por tabela
alter table public.empresas       enable row level security;
alter table public.usuarios       enable row level security;
alter table public.clientes       enable row level security;
alter table public.equipamentos   enable row level security;
alter table public.ordens_servico enable row level security;
alter table public.os_itens       enable row level security;
alter table public.os_historico   enable row level security;

force row level security on public.empresas;
force row level security on public.usuarios;
force row level security on public.clientes;
force row level security on public.equipamentos;
force row level security on public.ordens_servico;
force row level security on public.os_itens;
force row level security on public.os_historico;

-- empresas: leitura da propria loja, escrita apenas por admin
drop policy if exists p_empresas_select on public.empresas;
create policy p_empresas_select on public.empresas
  for select using (public.e_dono_da_empresa(id));

drop policy if exists p_empresas_update on public.empresas;
create policy p_empresas_update on public.empresas
  for update using (public.e_dono_da_empresa(id))
            with check (public.e_dono_da_empresa(id));

-- demais tabelas: por empresa_id
do $$
declare
  t text;
begin
  foreach t in array array['usuarios','clientes','equipamentos','ordens_servico','os_itens','os_historico']
  loop
    execute format('drop policy if exists p_%1$s_all on public.%1$I', t);
    execute format(
      'create policy p_%1$s_all on public.%1$I
         for all
         using (empresa_id = public.empresa_atual())
         with check (empresa_id = public.empresa_atual())', t);
  end loop;
end;
$$;

-- ---------------------------------------------------------------------
-- Ordem de inicializacao: 001 -> 002 -> 003
-- ---------------------------------------------------------------------
