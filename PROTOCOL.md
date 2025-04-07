# Protocolo de Comunicação — Sistema de Monitorização Oceânica

Este protocolo define a estrutura de mensagens trocadas entre os três níveis do sistema:

- WAVY (nó sensor)
- AGREGADOR (intermédio)
- SERVIDOR (centralizador)

## Mensagens WAVY → AGREGADOR

### `HELLO;WAVY_ID`
- Indica que uma WAVY iniciou comunicação.
- Exemplo: `HELLO;WAVY_001`

### `REGISTER;TIPO1,TIPO2,...`
- Lista os tipos de sensores da WAVY.
- Exemplo: `REGISTER;TemperaturaAgua,SalinidadeAgua`

### `DATA;TIPO;VALOR`
- Envia uma leitura sensorial.
- Exemplo: `DATA;TemperaturaAgua;20.5`

### `BYE`
- Encerra a sessão com o agregador.

---

## Mensagens AGREGADOR → WAVY

### `ACK`
- Resposta genérica a mensagens processadas com sucesso.

### `BYE_ACK`
- Confirmação de encerramento de sessão.

### `ERROR;MOTIVO`
- Erro com descrição do motivo.
- Exemplo: `ERROR;Tipo de sensor inválido`

---

## Mensagens AGREGADOR → SERVIDOR

### `FORWARD;AGG_DATA;TIPO;VALOR`
- Dados agregados prontos a ser armazenados.
- Exemplo: `FORWARD;AGG_DATA;TemperaturaAgua;21.3`

---

## Mensagens SERVIDOR → AGREGADOR

### `RECEIVED`
- Confirmação de receção dos dados agregados.

---

## Formatos de Dados

- `TIPO`: Nome do tipo de sensor (ex: `TemperaturaAgua`)
- `VALOR`: Valor decimal (ex: `21.3`)
- `WAVY_ID`: Identificador único da WAVY (ex: `WAVY_001`)
- `TIMESTAMP`: `yyyy-MM-ddTHH:mm:ss`

---

## Ficheiros de Configuração

### `config_wavys.csv`


### `TIPO.csv` (um por tipo de sensor)

---

## Lógica de Agregação

- Quando a WAVY envia `DATA`, os dados são acumulados.
- Quando o volume de dados atinge o limiar, calcula-se:
  - `media`, `soma`, etc. (definido por tipo)
- Envia-se `FORWARD;AGG_DATA;TIPO;VALOR` ao servidor.

---

## Segurança e Concorrência

- Escrita concorrente em ficheiros no servidor é protegida por locks/Mutex.
- Cada tipo de sensor tem seu ficheiro exclusivo.




(Todos os dados que temos ainda aparecem predefinidos por nós)
