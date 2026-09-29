using System.Data;
using Assistech.Shared.Dtos;
using Npgsql;

namespace Assistech.Data;

public interface IOrdemServicoRepository
{
    Task<PagedResult<OrdemServicoResumo>> ListarAsync(Guid empresaId, OrdemServicoFiltro filtro, CancellationToken ct = default);
    Task<OrdemServicoDto?> ObterAsync(Guid empresaId, Guid id, CancellationToken ct = default);
    Task<OrdemServicoDto> SalvarAsync(Guid empresaId, OrdemServicoInput input, string usuarioNome, CancellationToken ct = default);
    Task<OrdemServicoDto?> AlterarStatusAsync(Guid empresaId, Guid id, AlterarStatusRequest request, string usuarioNome, CancellationToken ct = default);
    Task ExcluirAsync(Guid empresaId, Guid id, CancellationToken ct = default);
    Task<TermoOrdemServicoDto?> ObterTermoAsync(Guid empresaId, Guid id, CancellationToken ct = default);
    Task<int> ContarPorStatusAsync(Guid empresaId, CancellationToken ct = default);
}

public sealed class OrdemServicoRepository : IOrdemServicoRepository
{
    private readonly IAssistechDb _db;

    public OrdemServicoRepository(IAssistechDb db) => _db = db;

    // ------------------------------------------------------------------ consulta

    public async Task<PagedResult<OrdemServicoResumo>> ListarAsync(Guid empresaId, OrdemServicoFiltro filtro, CancellationToken ct = default)
    {
        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, 200);
        var busca = (filtro.Busca ?? string.Empty).Trim();
        var status = (filtro.Status ?? string.Empty).Trim().ToLowerInvariant();

        var sql = $"""
            from public.ordens_servico o
            join public.clientes c on c.id = o.cliente_id
            join public.equipamentos e on e.id = o.equipamento_id
            where o.empresa_id = $1
            """;

        var filtros = new List<string>();
        if (busca.Length > 0)
            filtros.Add($"(c.nome ilike {Par(filtros.Count + 1)} or coalesce(c.telefone,'') ilike {Par(filtros.Count + 1)} or o.numero::text ilike {Par(filtros.Count + 1)} or e.marca ilike {Par(filtros.Count + 1)} or e.modelo ilike {Par(filtros.Count + 1)})");
        if (status.Length > 0 && status != "todos" && status != "todas")
            filtros.Add($"o.status::text = {Par(filtros.Count + 1)}");
        if (filtro.ClienteId.HasValue)
            filtros.Add($"o.cliente_id = {Par(filtros.Count + 1)}");
        if (filtro.De.HasValue)
            filtros.Add($"o.data_entrada >= {Par(filtros.Count + 1)}");
        if (filtro.Ate.HasValue)
            filtros.Add($"o.data_entrada < {Par(filtros.Count + 1)}");

        var where = filtros.Count == 0 ? sql : $"{sql} and {string.Join(" and ", filtros)}";

        var args = new List<object> { empresaId };
        if (busca.Length > 0) args.Add($"%{busca}%");
        if (status.Length > 0 && status != "todos" && status != "todas") args.Add(status);
        if (filtro.ClienteId.HasValue) args.Add(filtro.ClienteId.Value);
        if (filtro.De.HasValue) args.Add(filtro.De.Value.UtcDateTime);
        if (filtro.Ate.HasValue) args.Add(filtro.Ate.Value.UtcDateTime.AddDays(1).Date);

        await using var conn = await _db.OpenAsync(empresaId, ct);

        int total;
        await using (var cmd = new NpgsqlCommand($"select count(*) {where}", conn))
        {
            MontarParametros(cmd, args);
            total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        var selecao = $"""
            select o.id, o.numero, c.nome, coalesce(c.telefone, ''),
                   concat(e.marca, ' ', e.modelo), o.status::text,
                   greatest(coalesce(o.orcamento_valor, 0) - o.desconto, 0),
                   o.data_entrada, o.data_previsao,
                   (o.data_previsao is not null and o.data_previsao < now()
                    and o.status not in ('entregue','cancelada','pronta_entrega'))
            {where}
            order by o.data_entrada desc, o.numero desc
            limit {tamanho} offset {(pagina - 1) * tamanho}
            """;

        await using var cmdPagina = new NpgsqlCommand(selecao, conn);
        MontarParametros(cmdPagina, args);

        await using var reader = await cmdPagina.ExecuteReaderAsync(ct);
        var itens = await reader.LerListaAsync(r => new OrdemServicoResumo
        {
            Id = r.GetGuid(0),
            Numero = r.GetInt32(1),
            ClienteNome = r.GetString(2),
            ClienteTelefone = r.GetString(3),
            Equipamento = r.GetString(4),
            Status = r.GetString(5),
            ValorTotal = r.GetDecimal(6),
            DataEntrada = r.GetUtc(7),
            DataPrevisao = r.GetUtcNullable(8),
            Atrasada = r.GetBoolean(9)
        }, ct);

        return new PagedResult<OrdemServicoResumo>
        {
            Itens = itens,
            Total = total,
            Pagina = pagina,
            TamanhoPagina = tamanho
        };
    }

    public async Task<OrdemServicoDto?> ObterAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        var sql = """
            select o.id, o.numero, o.cliente_id, c.nome, coalesce(c.telefone, ''),
                   o.equipamento_id, concat(e.marca, ' ', e.modelo),
                   o.status::text, coalesce(o.tipo_servico, ''), coalesce(o.relato_cliente, ''),
                   coalesce(o.diagnostico, ''), coalesce(o.laudo_conclusao, ''),
                   coalesce(o.senha_equipamento, ''), coalesce(o.acessorios, ''),
                   o.orcamento_valor, o.desconto, o.garantia_dias,
                   o.data_entrada, o.data_previsao, o.data_conclusao,
                   coalesce(o.criado_por, ''), o.criado_em, o.atualizado_em,
                   (select coalesce(sum(i.quantidade * i.valor_unitario), 0) from public.os_itens i where i.ordem_servico_id = o.id),
                   (select coalesce(sum(i.quantidade * i.custo_unitario), 0) from public.os_itens i where i.ordem_servico_id = o.id),
                   (o.data_previsao is not null and o.data_previsao < now()
                    and o.status not in ('entregue','cancelada','pronta_entrega'))
              from public.ordens_servico o
              join public.clientes c on c.id = o.cliente_id
              join public.equipamentos e on e.id = o.equipamento_id
             where o.empresa_id = $1 and o.id = $2
            """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);

        OrdemServicoDto? ordem = null;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                var subtotal = reader.GetDecimal(23);
                var custo = reader.GetDecimal(24);
                var orcamento = reader.GetDecimalNullable(14);
                var desconto = reader.GetDecimal(15);
                var total = orcamento.HasValue ? Math.Max(orcamento.Value - desconto, 0) : subtotal;

                ordem = new OrdemServicoDto
                {
                    Id = reader.GetGuid(0),
                    Numero = reader.GetInt32(1),
                    ClienteId = reader.GetGuid(2),
                    ClienteNome = reader.GetString(3),
                    ClienteTelefone = reader.GetString(4),
                    EquipamentoId = reader.GetGuid(5),
                    EquipamentoDescricao = reader.GetString(6),
                    Status = reader.GetString(7),
                    TipoServico = reader.GetString(8),
                    RelatoCliente = reader.GetString(9),
                    Diagnostico = reader.GetString(10),
                    LaudoConclusao = reader.GetString(11),
                    SenhaEquipamento = reader.GetString(12),
                    Acessorios = reader.GetString(13),
                    OrcamentoValor = orcamento,
                    Desconto = desconto,
                    GarantiaDias = Convert.ToInt32(reader.GetValue(16)),
                    DataEntrada = reader.GetUtc(17),
                    DataPrevisao = reader.GetUtcNullable(18),
                    DataConclusao = reader.GetUtcNullable(19),
                    CriadoPor = reader.GetString(20),
                    CriadoEm = reader.GetUtc(21),
                    AtualizadoEm = reader.GetUtc(22),
                    CustoPecas = custo,
                    ValorTotal = total,
                    LucroEstimado = Math.Max(total - custo, 0),
                    Atrasada = reader.GetBoolean(25)
                };
            }
        }

        if (ordem is null) return null;

        ordem.Itens = await CarregarItensAsync(conn, id, ct);
        ordem.Historico = await CarregarHistoricoAsync(conn, id, ct);
        return ordem;
    }

    private static async Task<List<ItemOrdemServicoDto>> CarregarItensAsync(NpgsqlConnection conn, Guid osId, CancellationToken ct)
    {
        const string sql = """
            select id, tipo::text, descricao, quantidade, valor_unitario, custo_unitario
              from public.os_itens
             where ordem_servico_id = $1
             order by criado_em
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(osId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.LerListaAsync(r => new ItemOrdemServicoDto
        {
            Id = r.GetGuid(0),
            Tipo = r.GetString(1),
            Descricao = r.GetString(2),
            Quantidade = r.GetDecimal(3),
            ValorUnitario = r.GetDecimal(4),
            CustoUnitario = r.GetDecimal(5),
            Subtotal = r.GetDecimal(3) * r.GetDecimal(4)
        }, ct);
    }

    private static async Task<List<HistoricoOrdemServicoDto>> CarregarHistoricoAsync(NpgsqlConnection conn, Guid osId, CancellationToken ct)
    {
        const string sql = """
            select status_anterior::text, status_novo::text, coalesce(comentario, ''), coalesce(usuario_nome, ''), criado_em
              from public.os_historico
             where ordem_servico_id = $1
             order by criado_em desc
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(osId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.LerListaAsync(r => new HistoricoOrdemServicoDto
        {
            StatusAnterior = r.IsDBNull(0) ? string.Empty : r.GetString(0),
            StatusNovo = r.GetString(1),
            Comentario = r.GetString(2),
            UsuarioNome = r.GetString(3),
            CriadoEm = r.GetUtc(4)
        }, ct);
    }

    // ------------------------------------------------------------------ escrita

    public async Task<OrdemServicoDto> SalvarAsync(Guid empresaId, OrdemServicoInput input, string usuarioNome, CancellationToken ct = default)
    {
        var status = ValidarStatus(input.Status);
        var isNova = !input.Id.HasValue;
        var id = input.Id ?? Guid.NewGuid();

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var statusAnterior = isNova ? (string?)null : await LerStatusAsync(conn, tx, empresaId, id, ct);

        if (isNova)
        {
            const string sql = """
                insert into public.ordens_servico
                      (id, empresa_id, numero, cliente_id, equipamento_id, status, tipo_servico, relato_cliente,
                       diagnostico, senha_equipamento, acessorios, orcamento_valor, desconto, garantia_dias,
                       data_entrada, data_previsao, criado_por)
                values ($1, $2, public.proxima_os($2), $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14,
                        coalesce($15, now()), $16, $17)
                """;

            await using var cmdInsert = new NpgsqlCommand(sql, conn, tx);
            cmdInsert.Parameters.AddWithValue(id);
            cmdInsert.Parameters.AddWithValue(empresaId);
            cmdInsert.Parameters.AddWithValue(input.ClienteId ?? throw new InvalidOperationException("Informe o cliente da ordem de servico."));
            cmdInsert.Parameters.AddWithValue(input.EquipamentoId ?? throw new InvalidOperationException("Informe o equipamento da ordem de servico."));
            cmdInsert.Parameters.AddWithValue(status);
            cmdInsert.Parameters.AddWithValue(Nz(input.TipoServico));
            cmdInsert.Parameters.AddWithValue(Nz(input.RelatoCliente));
            cmdInsert.Parameters.AddWithValue(Nz(input.Diagnostico));
            cmdInsert.Parameters.AddWithValue(Nz(input.SenhaEquipamento));
            cmdInsert.Parameters.AddWithValue(Nz(input.Acessorios));
            cmdInsert.Parameters.AddWithValue(input.OrcamentoValor.HasValue ? input.OrcamentoValor.Value : (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue(Math.Max(input.Desconto, 0m));
            cmdInsert.Parameters.AddWithValue(Math.Clamp(input.GarantiaDias, 0, 3650));
            cmdInsert.Parameters.AddWithValue((object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue(input.DataPrevisao?.UtcDateTime ?? (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue(Nz(usuarioNome));

            await cmdInsert.ExecuteNonQueryAsync(ct);
        }
        else
        {
            const string sql = """
                update public.ordens_servico
                   set cliente_id = $3, equipamento_id = $4, status = $5, tipo_servico = $6,
                       relato_cliente = $7, diagnostico = $8, laudo_conclusao = $9,
                       senha_equipamento = $10, acessorios = $11, orcamento_valor = $12,
                       desconto = $13, garantia_dias = $14, data_previsao = $15,
                       data_conclusao = case when $5 = 'entregue' and data_conclusao is null
                                            then now() else data_conclusao end
                 where empresa_id = $1 and id = $2
                """;

            await using var cmdUpdate = new NpgsqlCommand(sql, conn, tx);
            cmdUpdate.Parameters.AddWithValue(empresaId);
            cmdUpdate.Parameters.AddWithValue(id);
            cmdUpdate.Parameters.AddWithValue(input.ClienteId ?? throw new InvalidOperationException("Informe o cliente da ordem de servico."));
            cmdUpdate.Parameters.AddWithValue(input.EquipamentoId ?? throw new InvalidOperationException("Informe o equipamento da ordem de servico."));
            cmdUpdate.Parameters.AddWithValue(status);
            cmdUpdate.Parameters.AddWithValue(Nz(input.TipoServico));
            cmdUpdate.Parameters.AddWithValue(Nz(input.RelatoCliente));
            cmdUpdate.Parameters.AddWithValue(Nz(input.Diagnostico));
            cmdUpdate.Parameters.AddWithValue(Nz(input.LaudoConclusao));
            cmdUpdate.Parameters.AddWithValue(Nz(input.SenhaEquipamento));
            cmdUpdate.Parameters.AddWithValue(Nz(input.Acessorios));
            cmdUpdate.Parameters.AddWithValue(input.OrcamentoValor.HasValue ? input.OrcamentoValor.Value : (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue(Math.Max(input.Desconto, 0m));
            cmdUpdate.Parameters.AddWithValue(Math.Clamp(input.GarantiaDias, 0, 3650));
            cmdUpdate.Parameters.AddWithValue(input.DataPrevisao?.UtcDateTime ?? (object)DBNull.Value);

            await cmdUpdate.ExecuteNonQueryAsync(ct);
        }

        await GravarItensAsync(conn, tx, empresaId, id, input.Itens, ct);

        if (isNova || statusAnterior != status)
        {
            const string sql = """
                insert into public.os_historico (empresa_id, ordem_servico_id, status_anterior, status_novo, comentario, usuario_nome)
                values ($1, $2, $3, $4, $5, $6)
                """;
            await using var cmdHist = new NpgsqlCommand(sql, conn, tx);
            cmdHist.Parameters.AddWithValue(empresaId);
            cmdHist.Parameters.AddWithValue(id);
            cmdHist.Parameters.AddWithValue(statusAnterior is null ? DBNull.Value : statusAnterior);
            cmdHist.Parameters.AddWithValue(status);
            cmdHist.Parameters.AddWithValue(isNova ? "Ordem de servico aberta." : "Ordem de servico atualizada.");
            cmdHist.Parameters.AddWithValue(Nz(usuarioNome));
            await cmdHist.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        return await ObterAsync(empresaId, id, ct)
               ?? throw new InvalidOperationException("Ordem de servico nao encontrada apos o salvamento.");
    }

    private static async Task GravarItensAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid empresaId, Guid osId, List<ItemOrdemServicoInput> itens, CancellationToken ct)
    {
        await using (var cmdDelete = new NpgsqlCommand("delete from public.os_itens where ordem_servico_id = $1", conn, tx))
        {
            cmdDelete.Parameters.AddWithValue(osId);
            await cmdDelete.ExecuteNonQueryAsync(ct);
        }

        if (itens is null || itens.Count == 0) return;

        const string sql = """
            insert into public.os_itens (empresa_id, ordem_servico_id, tipo, descricao, quantidade, valor_unitario, custo_unitario)
            values ($1, $2, $3, $4, $5, $6, $7)
            """;

        foreach (var item in itens)
        {
            if (string.IsNullOrWhiteSpace(item.Descricao)) continue;

            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue(empresaId);
            cmd.Parameters.AddWithValue(osId);
            cmd.Parameters.AddWithValue(ValidarTipoItem(item.Tipo));
            cmd.Parameters.AddWithValue(item.Descricao.Trim());
            cmd.Parameters.AddWithValue(item.Quantidade <= 0 ? 1m : item.Quantidade);
            cmd.Parameters.AddWithValue(Math.Max(item.ValorUnitario, 0m));
            cmd.Parameters.AddWithValue(Math.Max(item.CustoUnitario, 0m));

            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<OrdemServicoDto?> AlterarStatusAsync(Guid empresaId, Guid id, AlterarStatusRequest request, string usuarioNome, CancellationToken ct = default)
    {
        var novo = ValidarStatus(request.Status);

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var anterior = await LerStatusAsync(conn, tx, empresaId, id, ct);
        if (anterior is null)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        if (anterior == novo)
        {
            await tx.CommitAsync(ct);
            return await ObterAsync(empresaId, id, ct);
        }

        const string sql = """
            update public.ordens_servico
               set status = $3,
                   data_conclusao = case when $3 = 'entregue' and data_conclusao is null
                                        then now() else data_conclusao end
             where empresa_id = $1 and id = $2
            """;

        await using (var cmd = new NpgsqlCommand(sql, conn, tx))
        {
            cmd.Parameters.AddWithValue(empresaId);
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(novo);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string sqlHist = """
            insert into public.os_historico (empresa_id, ordem_servico_id, status_anterior, status_novo, comentario, usuario_nome)
            values ($1, $2, $3, $4, $5, $6)
            """;

        await using (var cmd = new NpgsqlCommand(sqlHist, conn, tx))
        {
            cmd.Parameters.AddWithValue(empresaId);
            cmd.Parameters.AddWithValue(id);
            cmd.Parameters.AddWithValue(anterior);
            cmd.Parameters.AddWithValue(novo);
            cmd.Parameters.AddWithValue(Nz(request.Comentario));
            cmd.Parameters.AddWithValue(Nz(usuarioNome));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return await ObterAsync(empresaId, id, ct);
    }

    public async Task ExcluirAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        const string sql = "delete from public.ordens_servico where empresa_id = $1 and id = $2";

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> ContarPorStatusAsync(Guid empresaId, CancellationToken ct = default)
    {
        const string sql = """
            select count(*) from public.ordens_servico
             where empresa_id = $1 and status not in ('entregue','cancelada')
            """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    // ------------------------------------------------------------------ termo

    public async Task<TermoOrdemServicoDto?> ObterTermoAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        var sql = """
            select o.id, o.numero, o.status::text, coalesce(o.tipo_servico, ''),
                   coalesce(o.relato_cliente, ''), coalesce(o.diagnostico, ''),
                   coalesce(o.laudo_conclusao, ''), coalesce(o.acessorios, ''),
                   o.orcamento_valor, o.desconto, o.garantia_dias,
                   o.data_entrada, o.data_previsao, o.data_conclusao,
                   c.nome, coalesce(c.documento, ''), coalesce(c.telefone, ''), coalesce(c.endereco, ''),
                   e.tipo::text, e.marca, e.modelo, coalesce(e.numero_serie, ''), coalesce(e.cor, ''),
                   em.nome, coalesce(em.documento, ''), coalesce(em.telefone, ''),
                   coalesce(em.whatsapp, ''), coalesce(em.endereco, ''), em.cor_primaria, (em.logo is not null)
              from public.ordens_servico o
              join public.clientes c on c.id = o.cliente_id
              join public.equipamentos e on e.id = o.equipamento_id
              join public.empresas em on em.id = o.empresa_id
             where o.empresa_id = $1 and o.id = $2
            """;

        TermoOrdemServicoDto? termo;
        decimal? orcamento;

        await using (var conn = await _db.OpenAsync(empresaId, ct))
        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue(empresaId);
            cmd.Parameters.AddWithValue(id);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            orcamento = reader.GetDecimalNullable(8);
            termo = new TermoOrdemServicoDto
            {
                OrdemServicoId = reader.GetGuid(0),
                Numero = reader.GetInt32(1),
                EmpresaId = empresaId,
                Status = reader.GetString(2),
                TipoServico = reader.GetString(3),
                RelatoCliente = reader.GetString(4),
                Diagnostico = reader.GetString(5),
                LaudoConclusao = reader.GetString(6),
                Acessorios = reader.GetString(7),
                Desconto = reader.GetDecimal(9),
                GarantiaDias = Convert.ToInt32(reader.GetValue(10)),
                DataEntrada = reader.GetUtc(11),
                DataPrevisao = reader.GetUtcNullable(12),
                DataConclusao = reader.GetUtcNullable(13),
                ClienteNome = reader.GetString(14),
                ClienteDocumento = reader.GetString(15),
                ClienteTelefone = reader.GetString(16),
                ClienteEndereco = reader.GetString(17),
                EquipamentoTipo = reader.GetString(18),
                EquipamentoMarca = reader.GetString(19),
                EquipamentoModelo = reader.GetString(20),
                EquipamentoNumeroSerie = reader.GetString(21),
                EquipamentoCor = reader.GetString(22),
                EmpresaNome = reader.GetString(23),
                EmpresaDocumento = reader.GetString(24),
                EmpresaTelefone = reader.GetString(25),
                Whatsapp = reader.GetString(26),
                EmpresaEndereco = reader.GetString(27),
                EmpresaCorPrimaria = reader.GetString(28),
                EmpresaTemLogo = reader.GetBoolean(29),
                EmitidoEm = DateTimeOffset.UtcNow
            };
            termo.EmpresaUrlLogo = termo.EmpresaTemLogo ? $"api/config/logo/{empresaId}" : string.Empty;
        }

        await using var conn2 = await _db.OpenAsync(empresaId, ct);
        termo.Itens = await CarregarItensAsync(conn2, id, ct);
        termo.Subtotal = termo.Itens.Sum(i => i.Subtotal);
        termo.ValorTotal = orcamento.HasValue
            ? Math.Max(orcamento.Value - termo.Desconto, 0)
            : termo.Subtotal;

        return termo;
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<string?> LerStatusAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid empresaId, Guid id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("select status::text from public.ordens_servico where empresa_id = $1 and id = $2", conn, tx);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);

        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    private static void MontarParametros(NpgsqlCommand cmd, List<object> args)
    {
        for (var i = 0; i < args.Count; i++)
            cmd.Parameters.AddWithValue($"${i + 1}", args[i]);
    }

    private static string Par(int indice) => $"${indice}";

    internal static string ValidarStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "em_analise" => "em_analise",
        "aguardando_pecas" => "aguardando_pecas",
        "aguardando_cliente" => "aguardando_cliente",
        "aguardando_aprovacao" => "aguardando_aprovacao",
        "em_reparo" => "em_reparo",
        "pronta_entrega" => "pronta_entrega",
        "entregue" => "entregue",
        "cancelada" => "cancelada",
        _ => "aberta"
    };

    private static string ValidarTipoItem(string? tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "peca" => "peca",
        "deslocamento" => "deslocamento",
        _ => "servico"
    };

    private static string Nz(string? valor) => string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();
}
