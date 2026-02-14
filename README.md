# Communication App

## How to send message

1. I send SMS from my iPhone to Android Phone.

2. Android receives SMS. If the SMS From my phone number (from my iPhone), Android SMS Receiver send a POST request /send. SMS text format is important. TARGET_TYPE|TO|MESSAGE. Example: SMS|+38098754321|Random Text

## How to receive message

1. Somebody sends message on specific platform.

2. Platform generates the webhook and sends POST request /receive/target_type. Example: /receive/teams. The message body is parsed from Teams. Retrieve the message body and sender.

3. Send SMS back to my iPhone with text: TEAMS|SENDER|MESSAGE_BODY