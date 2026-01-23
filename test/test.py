from claude_oauth import chat

messages = []

messages.append({"role": "user", "content": "My name is Yeongyu."})
response = chat(messages)
print(response.content)
messages.append({"role": "assistant", "content": response.content})

messages.append({"role": "user", "content": "What is my name?"})
response = chat(messages)
print(response.content)
