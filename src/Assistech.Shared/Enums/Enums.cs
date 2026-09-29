namespace Assistech.Shared.Enums;

public enum TipoEquipamento
{
    Celular = 0,
    Desktop = 1,
    Notebook = 2,
    Tablet = 3,
    Outro = 4
}

public enum StatusOrdemServico
{
    Aberta = 0,
    EmAnalise = 1,
    AguardandoPecas = 2,
    AguardandoCliente = 3,
    AguardandoAprovacao = 4,
    EmReparo = 5,
    ProntaEntrega = 6,
    Entregue = 7,
    Cancelada = 8
}

public enum TipoItemOrdemServico
{
    Peca = 0,
    Servico = 1,
    Deslocamento = 2
}

public enum PerfilUsuario
{
    Admin = 0,
    Tecnico = 1,
    Atendimento = 2
}
