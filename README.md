# Assistech

SaaS de gestão para assistência técnica: ordens de serviço, clientes, equipamentos,
personalização da loja (cor e logo) e termo de impressão.

| Projeto | Função |
| --- | --- |
| `src/Assistech.Shared` | DTOs, enums e o cliente HTTP da API (usado pela web e pelo desktop) |
| `src/Assistech.Data` | Repositórios Npgsql, contexto multi-tenant (`app.empresa_id`) e RLS |
| `src/Assistech.Api` | API ASP.NET Core (autenticação via Supabase Auth, Swagger, CORS) |
| `src/Assistech.Web` | Blazor Web App (`/entrar`, ordens, clientes, loja, termo) |
| `src/Assistech.Desktop` | WPF (login, ordens, clientes/equipamentos, loja, termo imprimível) |
| `db` | Scripts SQL de schema, RLS, papel da aplicação e seed |

## Pré-requisitos

- .NET 8 SDK
- Um projeto Supabase (PostgreSQL + Auth)
- Windows (o desktop é WPF)

## 1. Banco de dados

No **SQL Editor** do Supabase, nesta ordem:

```
db/001_schema.sql
db/002_rls.sql
select vault.create_secret('SUA_SENHA_FORTE', 'assistech_db');   -- antes do 003
db/003_app_role_and_seed.sql
```

O script 003 cria o papel `assistech_api` (que **não** tem `BYPASSRLS`) usando a senha
guardada no Vault, e semeia a primeira loja. Depois da primeira aplicação, mantenha
`Supabase:AplicarSchemaAutomaticamente` como `false`.

## 2. Configurar a API

As credenciais ficam em variáveis de ambiente, fora do repositório:

```powershell
$env:Supabase__Url="https://SEUREF.supabase.co"
$env:Supabase__AnonKey="eyJ..."
$env:Supabase__ConnectionString="postgresql://assistech_api:SUA_SENHA_FORTE@db.SEUREF.supabase.co:5432/postgres"
```

Use a conexão direta (porta `5432`), não o pooler: a API depende do `app.empresa_id` na sessão.
No painel do Supabase, copie a senha do projeto em **Project Settings → Database**.

## 3. Rodar

```powershell
dotnet run --project src\Assistech.Api      # http://localhost:5099 (Swagger em /swagger)
dotnet run --project src\Assistech.Web      # http://localhost:5141
dotnet run --project src\Assistech.Desktop
```

A web lê a URL da API em `Assistech:ApiBaseUrl`; o desktop, em `ApiBaseUrl` do `appsettings.json`.

## 4. Primeiro acesso

Crie um usuário em **Authentication → Users**. O primeiro login se vincula automaticamente
à única loja cadastrada, com perfil `admin`. Para vincular outro usuário a uma loja:

```sql
insert into public.usuarios (empresa_id, usuario_id, nome, email, perfil)
select (select id from public.empresas order by nome limit 1), id,
       'Administrador', email, 'admin'
  from auth.users where email = 'voce@exemplo.com';
```

## Como o isolamento entre lojas funciona

Cada transação da API abre a conexão com `set_config('app.empresa_id', ...)`; as políticas de
RLS filtram todas as tabelas por esse valor. Antes de existir contexto de loja (login),
duas funções `SECURITY DEFINER` resolvem o vínculo — `empresa_do_usuario()` e `empresa_unica()`.
São a única saída da RLS e devolvem apenas o id da loja.

## Termo de impressão (desktop)

O WPF em .NET 8 não expõe a paginação XPS do `FlowDocument` de forma utilizável, então o
termo é um layout WPF nativo em A4, renderizado com `RenderTargetBitmap` a 300 DPI e
impresso via `System.Windows.Forms.PrintDialog` + `PrintDocument`.

## Deploy no Render

O web é Blazor **Server** (SignalR/WebSocket) e a API é ASP.NET Core: nenhum dos dois roda
em hospedagem serverless (Vercel, Netlify Functions). No Render, ambos viram Web Services
com Docker.

1. Crie um Blueprint em **New → Blueprint**, apontando para este repositório. O `render.yaml`
   já declara os dois serviços e os campos que você precisa preencher.
2. Preencha as variáveis:

   | Serviço | Variável | Valor |
   | --- | --- | --- |
   | `assistech-api` | `Supabase__Url` | `https://SEUREF.supabase.co` |
   | `assistech-api` | `Supabase__AnonKey` | publishable key (`sb_publishable_...`) |
   | `assistech-api` | `Supabase__ConnectionString` | `postgresql://assistech_api:SENHA@db.SEUREF.supabase.co:5432/postgres` |
   | `assistech-api` | `Cors__Web__0` | URL pública do web |
   | `assistech-web` | `Assistech__ApiBaseUrl` | URL pública da API |

3. O banco é o Supabase, então o Blueprint não provisiona Postgres.

Detalhes que importam:

- `Dockerfile.api` e `Dockerfile.web` leem a porta da variável `PORT` (injetada pelo Render)
  e as duas apps leem `X-Forwarded-Proto`. Sem isso, o `UseHttpsRedirection` do web entra em
  laço de redirect, porque o app "vê" HTTP atrás do proxy que termina o TLS.
- O connection string em URI é convertido para `key=value` pelo
  `SupabaseOptions.NormalizarConnectionString()`, porque o Npgsql não aceita
  `postgresql://`. O `SSL Mode=Require` é automático, já que o certificado do Supabase não
  casa com o host `db.<ref>.supabase.co` na validação estrita.
- O Swagger só sobe em `ASPNETCORE_ENVIRONMENT=Development`.
- No plano gratuito, os serviços dormem após 15 min sem uso e a primeira requisição demora
  (partida fria do .NET). Para uso real, o plano pago elimina isso.

