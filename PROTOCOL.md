# Protocolo de Comunicação — Sistema de Monitorização Oceânica

Este protocolo define a estrutura das mensagens trocadas entre os três níveis do sistema:

- **WAVY** (nó sensor)
- **AGREGADOR** (intermediário)
- **SERVIDOR** (centralizador)

---

## Mensagens WAVY → AGREGADOR

- **HELLO;WAVY_ID**  
  Indica que uma WAVY iniciou comunicação.  
  Exemplo: `HELLO;WAVY_001`

- **REGISTER;TIPO1,TIPO2,...**  
  Lista os tipos de sensores disponíveis na WAVY.  
  Exemplo: `REGISTER;TemperaturaAgua,SalinidadeAgua`

- **DATA;TIPO;VALOR**  
  Envia uma leitura sensorial.  
  Exemplo: `DATA;TemperaturaAgua;20.5`

- **BYE**  
  Encerra a sessão com o Agregador.

---

## Mensagens AGREGADOR → WAVY

- **ACK**  
  Resposta genérica a mensagens processadas com sucesso.

- **BYE_ACK**  
  Confirmação de encerramento de sessão.

- **ERROR;MOTIVO**  
  Indica erro com descrição.  
  Exemplo: `ERROR;Tipo de sensor inválido`

---

## Mensagens AGREGADOR → SERVIDOR

- **FORWARD;AGG_DATA;WAVY_ID;TIPO;VALOR**  
  Dados agregados prontos a ser armazenados.  
  Exemplo: `FORWARD;AGG_DATA;WAVY_001;TemperaturaAgua;21.3`

---

## Mensagens SERVIDOR → AGREGADOR

- **RECEIVED**  
  Confirmação de receção dos dados agregados.

- **LIST**  
  Pede a lista de WAVYs ligadas.

- **STATS;TIPO**  
  Pede estatísticas dos últimos valores recebidos por cada WAVY para o tipo de sensor indicado.

- **RESET**  
  Solicita que o Agregador limpe os dados em memória.

---

## Mensagens AGREGADOR → SERVIDOR (respostas a comandos)

- **LIST_RESPONSE;WAVY1,WAVY2,...**  
  Resposta à mensagem `LIST`.

- **STATS_RESPONSE;WAVY_ID;VALOR**  
  Pode haver múltiplas linhas, uma por WAVY.

- **RESET_DONE**  
  Confirmação de reset concluído.

---

## Formatos de Dados

- **TIPO**: Nome do tipo de sensor (ex: `TemperaturaAgua`)
- **VALOR**: Valor decimal (ex: `21.3`)
- **WAVY_ID**: Identificador único da WAVY (ex: `WAVY_001`)
- **TIMESTAMP**: `yyyy-MM-ddTHH:mm:ss` (utilizado apenas internamente no servidor)

---

## Ficheiros de Configuração

- `config_wavys.csv` – Lista os sensores de cada WAVY.
- `TIPO.csv` – Um ficheiro por tipo de sensor com dados persistentes (opcional).
- `logs/dados_registados.txt` – Ficheiro principal onde o Servidor grava os dados recebidos.

---

## Lógica de Agregação

- Quando a WAVY envia `DATA`, o Agregador guarda o último valor para cada sensor/WAVY.
- Esses dados são imediatamente reencaminhados para o Servidor.
- Não há acumulação nem cálculo de média neste sistema.

---

## Segurança e Concorrência

- O Servidor usa `locks/Mutex` para proteger escrita concorrente no ficheiro `dados_registados.txt`.
- Cada tipo de sensor pode opcionalmente ter ficheiro dedicado.
- O sistema suporta múltiplas WAVYs em simultâneo de forma concorrente e fiável.

---
