# Compartilhamento de avaliações

A pessoa pode compartilhar apenas as próprias avaliações de música como uma
arte vertical, pronta para publicar em um Story e ajudar a divulgar o Somnia.

## Conteúdo da imagem

- Capa da música, quando disponível.
- Nome da música e artista.
- Nota e texto da avaliação.
- Gradiente inspirado no site, logo e nome Somnia.

## Implementação web

O botão aparece somente nas avaliações da pessoa autenticada. Nos detalhes da
música, ele fica junto às ações de editar e excluir a própria avaliação. Um
toque gera um PNG vertical de 1080 × 1920 e inicia o compartilhamento. A
avaliação é reduzida com reticências quando não couber na imagem. Se a capa não
puder ser carregada pelo navegador, a arte usa uma ilustração musical de
reserva. O botão inclui ícone e rótulo, com tamanho ajustado para telas móveis.

A integração nativa que abre diretamente o compositor de Stories fica para
depois do MVP.

Quando o navegador oferece compartilhamento de arquivos, o Somnia abre a folha
nativa e deixa a pessoa escolher o destino. A API web não permite selecionar
nem publicar diretamente no Story do Instagram. Se o compartilhamento de
arquivos não estiver disponível, o PNG é baixado para publicação manual.
