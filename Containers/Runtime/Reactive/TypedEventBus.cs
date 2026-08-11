using System;
using System.Collections.Generic;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// メッセージ型ごとに購読者を保持するコンテナ。
    /// <para>
    /// 中身は「型 → ハンドラのリスト」なので実体はコンテナだが、使うと設計が変わる道具でもある ——
    /// 送り手と受け手が互いを知らなくなる代わりに、<b>誰が何に反応するかがコードから読めなくなる</b>。
    /// プロジェクト全体の連絡手段にするのではなく、疎結合が本当に要る境界に限って使うのがよい。
    /// </para>
    /// <para>
    /// <see cref="Subscribe{TMessage}"/> は解除用の <see cref="IDisposable"/> を返すので、
    /// <see cref="DisposableBag"/> に入れておけば <c>OnDestroy</c> でまとめて外れる。
    /// </para>
    /// </summary>
    public sealed class TypedEventBus
    {
        /// <summary>型ごとの購読者。ジェネリック型の静的性質を使わず、インスタンスごとに分離する。</summary>
        private sealed class Channel<TMessage>
        {
            public readonly List<Action<TMessage>> Handlers = new List<Action<TMessage>>();
        }

        private readonly Dictionary<Type, object> _channels = new Dictionary<Type, object>();

        /// <summary>購読されているメッセージ型の数。</summary>
        public int ChannelCount => _channels.Count;

        public IDisposable Subscribe<TMessage>(Action<TMessage> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var channel = GetChannel<TMessage>();
            channel.Handlers.Add(handler);

            return Disposable.Create(() => channel.Handlers.Remove(handler));
        }

        /// <summary>1 回受け取ったら自動的に解除される購読。</summary>
        public IDisposable SubscribeOnce<TMessage>(Action<TMessage> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            IDisposable subscription = null;
            var invoked = false;

            subscription = Subscribe<TMessage>(message =>
            {
                if (invoked) return;

                invoked = true;
                subscription?.Dispose();
                handler(message);
            });

            return subscription;
        }

        /// <summary>
        /// 購読者全員に配る。ハンドラ内での購読・解除に耐えるよう、配布前にリストを写す。
        /// </summary>
        public void Publish<TMessage>(TMessage message)
        {
            if (!_channels.TryGetValue(typeof(TMessage), out var channelObject)) return;

            var handlers = ((Channel<TMessage>)channelObject).Handlers;
            if (handlers.Count == 0) return;

            using var snapshot = TempList<Action<TMessage>>.Rent();
            for (var i = 0; i < handlers.Count; i++) snapshot.List.Add(handlers[i]);

            for (var i = 0; i < snapshot.List.Count; i++)
            {
                try
                {
                    snapshot.List[i]?.Invoke(message);
                }
                catch (Exception exception)
                {
                    // 1 人の失敗で残りの配布が止まらないようにする。
                    Debug.LogException(exception);
                }
            }
        }

        public int SubscriberCount<TMessage>() =>
            _channels.TryGetValue(typeof(TMessage), out var channel) ? ((Channel<TMessage>)channel).Handlers.Count : 0;

        public void Clear<TMessage>()
        {
            if (_channels.TryGetValue(typeof(TMessage), out var channel)) ((Channel<TMessage>)channel).Handlers.Clear();
        }

        public void Clear() => _channels.Clear();

        private Channel<TMessage> GetChannel<TMessage>()
        {
            if (_channels.TryGetValue(typeof(TMessage), out var existing)) return (Channel<TMessage>)existing;

            var channel = new Channel<TMessage>();
            _channels[typeof(TMessage)] = channel;
            return channel;
        }
    }
}
