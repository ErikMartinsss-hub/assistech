// O cookie de sessao precisa ser gravado na resposta que chega ao NAVEGADOR.
// Um POST server-to-server (HttpClient dentro do circuito) traz o Set-Cookie de
// volta para o servidor, que o descarta - e o F5 seguinte chega sem sessao.
window.assistech = {
  async sessao(acao, corpo) {
    const resposta = await fetch(acao, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        // Header customizado: um site de terceiros precisaria de preflight de
        // CORS para envia-lo, e este host nao responde preflight. E o que barra
        // o CSRF de registrar uma sessao alheia.
        'X-Assistech-Sessao': '1'
      },
      body: corpo ? JSON.stringify(corpo) : null
    });

    if (!resposta.ok) throw new Error(await resposta.text());
  }
};
