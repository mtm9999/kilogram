﻿
﻿using System;
﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
﻿using System.ComponentModel;
﻿using System.Linq;
﻿using System.Runtime.CompilerServices;
﻿using System.Threading.Tasks;
﻿using System.Windows;
﻿using Microsoft.Phone.Reactive;
﻿using System.IO;
﻿using Telegram.Annotations;
﻿using Telegram.Core.Logging;
﻿using Telegram.Model.Internal;
﻿using Telegram.MTProto;
﻿using Telegram.Utils;

namespace Telegram.Model.Wrappers {
    public class DialogModel: INotifyPropertyChanged {
        private static readonly Logger logger = LoggerFactory.getLogger(typeof(DialogModel));
        private DialogConstructor dialog;
        private ObservableCollectionUI<MessageModel> messages;
        private TelegramSession session;

        private string MyNamePattern {
            get {
                return "You";
            }
        }

        public DialogModel(Dialog dialog, TelegramSession session, Dictionary<int, MessageModel> messagesMap) {
            this.dialog = (DialogConstructor) dialog;
            this.session = session;
            this.messages = new ObservableCollectionUI<MessageModel>();
            this.messages.Add(messagesMap[this.dialog.top_message]);

            SubscribeToDialog();
        }

        // FIXME: review ctor params !!
        public DialogModel(MessageModelDelivered topMessage, TelegramSession session) {
            this.dialog = (DialogConstructor) TL.dialog(topMessage.Peer, topMessage.Id, 1);
            this.session = session;
            this.messages = new ObservableCollectionUI<MessageModel>();
            this.messages.Add(topMessage);
            
            SubscribeToDialog();
        }

        private void SubscribeToDialog() {
            switch (dialog.peer.Constructor) {
                case Constructor.peerChat:
                    var peerChat = dialog.peer as PeerChatConstructor;
                    var chat = session.GetChat(peerChat.chat_id);

                    chat.PropertyChanged += DialogOnPropertyChanged;

                    break;
                case Constructor.peerUser:
                    var peerUser = dialog.peer as PeerUserConstructor;
                    var user = session.GetUser(peerUser.user_id);

                    user.PropertyChanged += DialogOnPropertyChanged;

                    break;
            }
        }

        // proxy method from holded dialog object (user or chat)
        private void DialogOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            logger.debug("Property changing [Title] " + propertyChangedEventArgs.PropertyName);

            if (propertyChangedEventArgs.PropertyName == "Title"
                || propertyChangedEventArgs.PropertyName == "FullName") {
                OnPropertyChanged("Title");
            } else if (propertyChangedEventArgs.PropertyName == "Status") {
                OnPropertyChanged("Status");
            }
        }

        public DialogModel(TelegramSession session, BinaryReader reader) {
            this.session = session;
            Read(reader);
        }

        private Dialog RawDialog {
            get {
                return dialog;
            }
        }

        public int Id {
            get {
                switch (dialog.peer.Constructor) {
                    case Constructor.peerChat:
                        var peerChat = dialog.peer as PeerChatConstructor;
                        return peerChat.chat_id;

                    case Constructor.peerUser:
                        var peerUser = dialog.peer as PeerUserConstructor;
                        return peerUser.user_id;
                }

                return -1;
            }
        }

        public Peer Peer {
            get {
                return dialog.peer;
            }
        }

        private InputPeer InputPeer {
            get {
                if (dialog.Constructor == Constructor.peerChat) {
                    return TL.inputPeerChat(((PeerChatConstructor) dialog.peer).chat_id);
                }

                var peerUser = dialog.peer as PeerUserConstructor;
                var user = session.GetUser(peerUser.user_id);

                return user.InputPeer;
            }
        }

        public string Status {
            get {
                string status = "";
                switch (dialog.peer.Constructor) {
                    case Constructor.peerChat:
                        var peerChat = dialog.peer as PeerChatConstructor;
                        var chat = session.GetChat(peerChat.chat_id);
                        return chat.Status;

                    case Constructor.peerUser:
                        var peerUser = dialog.peer as PeerUserConstructor;
                        var user = session.GetUser(peerUser.user_id);
                        return user.Status;
                }

                return status;
            }
        }

        public string Title {
            get {
                string title = "";
                switch (dialog.peer.Constructor) {
                    case Constructor.peerChat:
                        var peerChat = dialog.peer as PeerChatConstructor;
                        // check null
                        var chatModel = session.GetChat(peerChat.chat_id);
                        title = chatModel.Title;                        
                        break;

                    case Constructor.peerUser:
                        var peerUser = dialog.peer as PeerUserConstructor;
                        var userModel = session.GetUser(peerUser.user_id);
                        title = userModel.FullName;
                        break;
                }

                return title;
            }
        }

        public ObservableCollection<MessageModel> Messages {
            get {
                if(messages == null) {
                    messages = new ObservableCollectionUI<MessageModel>();
                    MessagesRequest();
                }

                return messages;
            }
        }


        private async Task MessagesRequest() {
            messages_Messages loadedMessages = await session.Api.messages_getHistory(TLStuff.PeerToInputPeer(dialog.peer), 0, -1, 100);
            List<Message> messagesList;
            List<Chat> chatsList;
            List<User> usersList;

            switch(loadedMessages.Constructor) {
                case Constructor.messages_messages:
                    chatsList = ((Messages_messagesConstructor) loadedMessages).chats;
                    messagesList = ((Messages_messagesConstructor) loadedMessages).messages;
                    usersList = ((Messages_messagesConstructor) loadedMessages).users;
                    break;
                case Constructor.messages_messagesSlice:
                    chatsList = ((Messages_messagesSliceConstructor) loadedMessages).chats;
                    messagesList = ((Messages_messagesSliceConstructor) loadedMessages).messages;
                    usersList = ((Messages_messagesSliceConstructor) loadedMessages).users;
                    break;
                default:
                    return;
            }


            foreach(var user in usersList) {
                session.SaveUser(user);
            }

            foreach(var chat in chatsList) {
                session.SaveChat(chat);
            }

            foreach(var message in messagesList) {
                messages.Add(new MessageModelDelivered(message));
            }
        }

        public string Preview {
            get {

                MessageModel messageModel = messages.Last();
                if (messageModel.Delivered == false) {
                    return ((MessageModelUndelivered) messageModel).Text;
                }
                
                string preview = "";
                var topMessage = ((MessageModelDelivered) messageModel).RawMessage;

                switch (topMessage.Constructor) {
                    case Constructor.message:
                        preview = ((MessageConstructor)topMessage).message;
                        break;
                    case Constructor.messageForwarded:
                        preview = ((MessageForwardedConstructor)topMessage).message;
                        break;
                    case Constructor.messageService:
                        preview = "SERVICE";
                        break;
                    default:
                        throw new InvalidDataException("invalid constructor");
                }

                return preview;
            }
        }

        public void Write(BinaryWriter writer) {
            dialog.Write(writer);
            if(messages == null) {
                writer.Write(0);
            } else {
                int messagesIndexStart = 0;
                int messagesCount = messages.Count;
                if(messagesCount > 100) {
                    messagesCount = 100;
                    messagesIndexStart = messagesCount - 100;
                }
                writer.Write(messagesCount);
                for(int i = messagesIndexStart; i < messagesIndexStart + messagesCount; i++) {
                    messages[i].Write(writer);
                }

                logger.info("saved {0} messages", messagesCount);
            }
        }

        public void Read(BinaryReader reader) {
            logger.info("loading dialog");
            dialog = new DialogConstructor();
            reader.ReadInt32();
            dialog.Read(reader);
            int messagesCount = reader.ReadInt32();
            logger.info("loading {0} messages", messagesCount);
            messages = new ObservableCollectionUI<MessageModel>();
            for(int i = 0; i < messagesCount; i++) {
                int type = reader.ReadInt32();
                if (type == 1) {
                    // delivered
                    messages.Add(new MessageModelDelivered(reader));
                }
                else {
                    // undelivered
                    messages.Add(new MessageModelUndelivered(reader));
                }
            }

            logger.info("loaded {0} messages", messagesCount);
        }

        public bool IsChat {
            get {
                return dialog.peer.Constructor == Constructor.peerChat;
            }
        }

        public string LastActivityUserName {
            get {
                MessageModel messageModel = messages.Last();

                if (messageModel.Delivered == false) {
                    return MyNamePattern;
                }

                var messageModelDelivered = (MessageModelDelivered) messageModel;

                var topMessage = messageModelDelivered.RawMessage;
                UserModel user = null;
                switch (topMessage.Constructor) {
                    case Constructor.message:
                        user = session.GetUser(((MessageConstructor)topMessage).from_id);
                        break;
                    case Constructor.messageForwarded:
                        user = session.GetUser(((MessageForwardedConstructor)topMessage).from_id);
                        break;
                    case Constructor.messageService:
                        break;
                    default:
                        throw new InvalidDataException("invalid constructor");
                }
                
                string fullName = "service user";
                
                if (user == null) {
                    return fullName;
                }

                return user.FullName;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public void ProcessNewMessage(MessageModel messageModel) {
            logger.info("processing message and adding to observable collection");
                messages.Add(messageModel);
        }

        public async Task SendMessage(string message) {
            long randomId = Helpers.GenerateRandomLong();

            messages.Add(new MessageModelUndelivered() {
                MessageType = MessageModelUndelivered.Type.Text,
                Text = message,
                Timestamp = DateTime.Now
            });

            messages_SentMessage sentMessage = await TelegramSession.Instance.Api.messages_sendMessage(InputPeer, message, randomId);

            switch (sentMessage.Constructor) {
                case Constructor.messages_sentMessage:
                    // replace Undelivered with delivered
                    break;
                case Constructor.messages_sentMessageLink:
                    // ???
                    break;
            }
        }
    }
}
