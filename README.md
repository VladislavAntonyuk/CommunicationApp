# Communication App

## How to send message

1. I send SMS from my iPhone to Android Phone.

2. Android receives SMS. If the SMS From my phone number (from my iPhone), Android SMS Receiver send a POST request /send. SMS text format is important. TARGET_TYPE|TO|MESSAGE. Example: SMS|+38098754321|Random Text

## How to receive message

1. Somebody sends message on specific platform.

2. Platform generates the webhook and sends POST request /notify/target_type. Example: /notify/Email. The message body is parsed from WebHook. Retrieve the message body and sender.

3. Store the webhook message body and sender in the database.

## How to notify me (Send SMS back to iPhone)

1. Android app periodically pings the web api sending GET /receive request.

1. For each message send SMS back to my iPhone with text.

## Demo

![Demo](demo.mp4)

