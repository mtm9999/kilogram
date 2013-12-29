﻿using Telegram.MTProto;

namespace Telegram.Model.Wrappers {
    public class DialogModel {
        private DialogConstructor dialog;
        private IMessageProvider messageProvider;
        private IUserProvider userProvider;
        private IChatProvider chatProvider;

        public DialogModel(Dialog dialog, IMessageProvider messageProvider, IUserProvider userProvider, IChatProvider chatProvider) {
            this.dialog = (DialogConstructor) dialog;
            this.messageProvider = messageProvider;
            this.userProvider = userProvider;
            this.chatProvider = chatProvider;
        }

        public Dialog RawDialog {
            get {
                return dialog;
            }
        }

        public string Title {
            get {
                string title = "";
                switch (dialog.peer.Constructor) {
                    case Constructor.peerChat:
                        var peerChat = dialog.peer as PeerChatConstructor;
                        // check null
                        var chatModel = chatProvider.GetChat(peerChat.chat_id);
                        title = chatModel.Title;                        
                        break;

                    case Constructor.peerUser:
                        var peerUser = dialog.peer as PeerUserConstructor;
                        var userModel = userProvider.GetUser(peerUser.user_id);
                        title = userModel.FullName;
                        break;
                }

                return title;
            }
        }
    }
}