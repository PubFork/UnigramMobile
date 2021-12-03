﻿using System.Collections.Generic;

namespace Unigram.Navigation.Services
{
    public class NavigationState : Dictionary<string, object>
    {
        public static NavigationState GetChatMember(long chatId, long userId)
        {
            return new NavigationState { { "chatId", chatId }, { "userId", userId } };
        }
    }
}
