-- =====================================================================
-- Assistech - Papel da aplicacao + seed inicial
-- Execute DEPOIS de 001 e 002.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Login da aplicacao
-- A senha vem do Vault do Supabase, para nao ficar no repositorio.
-- Rode ANTES deste script, no SQL Editor:
--
--   select vault.create_secret('SUA_SENHA_FORTE', 'assistech_db');
--
-- Depois use na API (Project Settings > Database > Connection string > URI):
--   postgresql://assistech_api:SUA_SENHA_FORTE@db.<ref>.supabase.co:5432/postgres
--
-- Use a conexão direta (porta 5432), nao o pooler: a API precisa do
-- app.empresa_id na sessao.
-- ---------------------------------------------------------------------
do $$
declare
  senha text;
begin
  select decrypted_secret into senha
    from vault.decrypted_secrets
   where name = 'assistech_db';

  if senha is null then
    raise exception 'Crie o segredo antes: select vault.create_secret(''SUA_SENHA_FORTE'', ''assistech_db'');';
  end if;

  if exists (select 1 from pg_roles where rolname = 'assistech_api') then
    execute format('alter role assistech_api login password %L', senha);
  else
    execute format('create role assistech_api login password %L', senha);
  end if;
end;
$$;

grant assistech_app to assistech_api;

-- ---------------------------------------------------------------------
-- company_id precisa ser lido em contexto de RLS
-- ---------------------------------------------------------------------
alter table public.ordens_servico  alter column empresa_id set default public.empresa_atual();
alter table public.clientes        alter column empresa_id set default public.empresa_atual();
alter table public.equipamentos    alter column empresa_id set default public.empresa_atual();
alter table public.os_itens        alter column empresa_id set default public.empresa_atual();
alter table public.os_historico    alter column empresa_id set default public.empresa_atual();

-- =====================================================================
-- SEED: primeira loja
-- O primeiro usuario que entrar vira admin dela automaticamente
-- (POST /api/auth/login), desde que exista apenas uma loja.
-- Para vincular um usuario a uma loja especifica, rode no SQL Editor:
--
--   insert into public.usuarios (empresa_id, usuario_id, nome, email, perfil)
--   select (select id from public.empresas order by nome limit 1), id,
--          'Administrador', email, 'admin'
--     from auth.users where email = 'voce@exemplo.com';
--
-- O usuario precisa existir antes: crie em Authentication > Users.
-- =====================================================================

insert into public.empresas (nome, documento, telefone, cor_primaria)
select 'Minha Assistencia Tecnica', '', '', '#0d6efd'
where not exists (select 1 from public.empresas);
